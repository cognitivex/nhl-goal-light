using System.Text.Json.Serialization;

namespace NhlGoalLight.Hue;

/// <summary>
/// Hue light state payload. Field names match the Hue CLIP v1 API (lowercase).
/// Only non-null fields are serialized; everything else is left unchanged on the bulb.
/// </summary>
public sealed record HueLightState
{
    [JsonPropertyName("on")] public bool? On { get; init; }

    /// <summary>Brightness 1..254.</summary>
    [JsonPropertyName("bri")] public int? Bri { get; init; }

    /// <summary>Hue 0..65535.</summary>
    [JsonPropertyName("hue")] public int? Hue { get; init; }

    /// <summary>Saturation 0..254.</summary>
    [JsonPropertyName("sat")] public int? Sat { get; init; }

    /// <summary>CIE 1931 xy color coordinates, length 2.</summary>
    [JsonPropertyName("xy")] public double[]? Xy { get; init; }

    /// <summary>Mireds 153..500 (cool..warm).</summary>
    [JsonPropertyName("ct")] public int? Ct { get; init; }

    /// <summary>Transition time in deciseconds (1/10 sec). 0 = instant.</summary>
    [JsonPropertyName("transitiontime")] public int? TransitionTime { get; init; }

    // Deliberately uninteresting beige for opponent goals. Low saturation, mid brightness.
    // Stays a static — opponent-flash is intentionally team-agnostic.
    public static readonly HueLightState Beige = new() { On = true, Bri = 140, Hue = 6000, Sat = 90, TransitionTime = 0 };

    public static readonly HueLightState Off = new() { On = false, TransitionTime = 4 };
}
