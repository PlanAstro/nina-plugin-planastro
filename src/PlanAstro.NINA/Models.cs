using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlanAstro.NINA {

    /// <summary>One framed target sent from PlanAstro (format version 1, see the spec).</summary>
    public class SentTarget {
        [JsonPropertyName("v")] public int Version { get; set; } = 1;
        [JsonPropertyName("id")] public string Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }

        /// <summary>J2000 right ascension in degrees.</summary>
        [JsonPropertyName("ra")] public double Ra { get; set; }

        /// <summary>J2000 declination in degrees.</summary>
        [JsonPropertyName("dec")] public double Dec { get; set; }

        /// <summary>Position angle in degrees, east of north.</summary>
        [JsonPropertyName("rotation")] public double Rotation { get; set; }

        [JsonPropertyName("mosaic")] public Mosaic Mosaic { get; set; }
        [JsonPropertyName("panels")] public List<Panel> Panels { get; set; }
        [JsonPropertyName("fov")] public FieldOfView Fov { get; set; }
        [JsonPropertyName("rig")] public Rig Rig { get; set; }
        [JsonPropertyName("filters")] public List<FilterPlan> Filters { get; set; }
        [JsonPropertyName("note")] public string Note { get; set; }
        [JsonPropertyName("sentAt")] public DateTimeOffset SentAt { get; set; }

        [JsonIgnore] public bool IsMosaic => Mosaic != null && (Mosaic.Columns > 1 || Mosaic.Rows > 1);

        [JsonIgnore]
        public string Summary {
            get {
                var parts = new List<string> { $"RA {Ra:0.000}°, Dec {Dec:+0.000;-0.000}°", $"rotation {Rotation:0.#}°" };
                if (IsMosaic) parts.Add($"{Mosaic.Columns} × {Mosaic.Rows} mosaic, {Mosaic.OverlapPercent:0}% overlap");
                if (Rig != null && Rig.FocalLengthMm > 0) parts.Add($"{Rig.FocalLengthMm:0} mm");
                return string.Join(" · ", parts);
            }
        }

        [JsonIgnore] public string SentAtLocal => SentAt == default ? "" : SentAt.ToLocalTime().ToString("MMM d, h:mm tt");
    }

    public class Mosaic {
        [JsonPropertyName("columns")] public int Columns { get; set; } = 1;
        [JsonPropertyName("rows")] public int Rows { get; set; } = 1;
        [JsonPropertyName("overlapPercent")] public double OverlapPercent { get; set; } = 15;
    }

    public class Panel {
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("ra")] public double Ra { get; set; }
        [JsonPropertyName("dec")] public double Dec { get; set; }
    }

    public class FieldOfView {
        [JsonPropertyName("widthDeg")] public double WidthDeg { get; set; }
        [JsonPropertyName("heightDeg")] public double HeightDeg { get; set; }
    }

    public class Rig {
        [JsonPropertyName("telescope")] public string Telescope { get; set; }
        [JsonPropertyName("reducer")] public double Reducer { get; set; } = 1;
        [JsonPropertyName("focalLengthMm")] public double FocalLengthMm { get; set; }
        [JsonPropertyName("camera")] public string Camera { get; set; }
        [JsonPropertyName("pixelSizeUm")] public double PixelSizeUm { get; set; }
    }

    public class FilterPlan {
        [JsonPropertyName("filter")] public string Filter { get; set; }
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("seconds")] public double Seconds { get; set; }
    }

    internal class PairRequest {
        [JsonPropertyName("code")] public string Code { get; set; }
        [JsonPropertyName("deviceName")] public string DeviceName { get; set; }
        [JsonPropertyName("pluginVersion")] public string PluginVersion { get; set; }
    }

    internal class PairResponse {
        [JsonPropertyName("token")] public string Token { get; set; }
        [JsonPropertyName("account")] public string Account { get; set; }
    }

    internal class QueueResponse {
        [JsonPropertyName("targets")] public List<SentTarget> Targets { get; set; } = new();
    }

    internal class AckRequest {
        [JsonPropertyName("status")] public string Status { get; set; }
        [JsonPropertyName("reason")] public string Reason { get; set; }
    }
}
