using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PlanAstro.NINA {

    /// <summary>Talks to PlanAstro's NINA endpoints. Outgoing HTTPS only; nothing listens on this PC.</summary>
    internal class PlanAstroClient {
        private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };

        private readonly Func<string> baseUrl;
        private readonly Func<string> token;
        private readonly string pluginVersion;

        public PlanAstroClient(Func<string> baseUrl, Func<string> token, string pluginVersion) {
            this.baseUrl = baseUrl;
            this.token = token;
            this.pluginVersion = pluginVersion;
        }

        private Uri Url(string path) => new(new Uri(baseUrl().TrimEnd('/') + "/"), path.TrimStart('/'));

        private HttpRequestMessage Request(HttpMethod method, string path) {
            var req = new HttpRequestMessage(method, Url(path));
            req.Headers.UserAgent.ParseAdd($"PlanAstroNINA/{pluginVersion}");
            var t = token();
            if (!string.IsNullOrEmpty(t)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
            return req;
        }

        public async Task<PairResponse> Pair(string code, string deviceName, CancellationToken ct) {
            using var req = Request(HttpMethod.Post, "api/nina/pair");
            req.Content = JsonContent.Create(new PairRequest { Code = code.Trim().ToUpperInvariant(), DeviceName = deviceName, PluginVersion = pluginVersion });
            using var res = await http.SendAsync(req, ct);
            if (res.StatusCode == HttpStatusCode.NotFound || res.StatusCode == HttpStatusCode.BadRequest || res.StatusCode == HttpStatusCode.Gone)
                throw new PlanAstroException("That code didn't work. Codes expire after 10 minutes and work once; get a new one in PlanAstro (Menu > Connect NINA).");
            if ((int)res.StatusCode == 429)
                throw new PlanAstroException("Too many attempts. Wait a minute, then try a new code.");
            await EnsureOk(res);
            var body = await res.Content.ReadFromJsonAsync<PairResponse>(cancellationToken: ct);
            if (body == null || string.IsNullOrEmpty(body.Token)) throw new PlanAstroException("PlanAstro didn't return a device token.");
            return body;
        }

        public async Task<QueueResponse> GetQueue(CancellationToken ct) {
            using var req = Request(HttpMethod.Get, "api/nina/queue");
            using var res = await http.SendAsync(req, ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized || res.StatusCode == HttpStatusCode.Forbidden)
                throw new NotPairedException();
            await EnsureOk(res);
            var result = new QueueResponse();
            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)); // not disposed: targets keep JsonElements (panels, filters)
            if (!doc.RootElement.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Array) return result;
            // Read each target on its own, so one target in an unexpected shape never blocks the rest.
            foreach (var el in targets.EnumerateArray()) {
                var id = el.ValueKind == JsonValueKind.Object && el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
                try {
                    var t = el.Deserialize<SentTarget>(Lenient);
                    if (t != null) result.Targets.Add(t);
                } catch (Exception ex) {
                    Log.Error($"Could not read target {id ?? "(no id)"}", ex);
                    if (!string.IsNullOrEmpty(id)) result.Rejected.Add((id, "Plugin could not read this target: " + ex.Message));
                }
            }
            return result;
        }

        /// <summary>Tolerant reading: numbers may arrive as strings, null or with decimals.</summary>
        internal static readonly JsonSerializerOptions Lenient = new() {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new LenientDouble(), new LenientInt() }
        };

        private sealed class LenientDouble : JsonConverter<double> {
            public override double Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => r.TokenType switch {
                JsonTokenType.Number => r.GetDouble(),
                JsonTokenType.String => double.TryParse(r.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0,
                _ => 0
            };
            public override void Write(Utf8JsonWriter w, double v, JsonSerializerOptions o) => w.WriteNumberValue(v);
        }

        private sealed class LenientInt : JsonConverter<int> {
            public override int Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => r.TokenType switch {
                JsonTokenType.Number => r.TryGetInt32(out var i) ? i : (int)Math.Round(r.GetDouble()),
                JsonTokenType.String => double.TryParse(r.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? (int)Math.Round(d) : 0,
                _ => 0
            };
            public override void Write(Utf8JsonWriter w, int v, JsonSerializerOptions o) => w.WriteNumberValue(v);
        }

        public async Task Ack(string id, string status, string reason, CancellationToken ct) {
            using var req = Request(HttpMethod.Post, $"api/nina/queue/{Uri.EscapeDataString(id)}/ack");
            req.Content = JsonContent.Create(new AckRequest { Status = status, Reason = reason });
            using var res = await http.SendAsync(req, ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized || res.StatusCode == HttpStatusCode.Forbidden)
                throw new NotPairedException();
            await EnsureOk(res);
        }

        private static async Task EnsureOk(HttpResponseMessage res) {
            if (res.IsSuccessStatusCode) return;
            var text = "";
            try { text = await res.Content.ReadAsStringAsync(); } catch { }
            if (text.Length > 200) text = text.Substring(0, 200);
            throw new PlanAstroException($"PlanAstro answered {(int)res.StatusCode}. {text}".Trim());
        }
    }

    public class PlanAstroException : Exception {
        public PlanAstroException(string message) : base(message) { }
    }

    /// <summary>The token was revoked in PlanAstro or is no longer valid.</summary>
    public class NotPairedException : PlanAstroException {
        public NotPairedException() : base("This PC was disconnected in PlanAstro. Pair again with a new code.") { }
    }
}
