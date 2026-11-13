using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
            return await res.Content.ReadFromJsonAsync<QueueResponse>(cancellationToken: ct) ?? new QueueResponse();
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
