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

    // --- Habs brand colors, approximated in CIE xy for gamut-C bulbs (Color & White Ambiance). ---
    // Adjust if your bulbs are a different gamut and the color looks off.
    public static readonly HueLightState HabsRed   = new() { On = true, Bri = 254, Xy = [0.6750, 0.3220], TransitionTime = 0 };
    public static readonly HueLightState HabsBlue  = new() { On = true, Bri = 254, Xy = [0.1500, 0.0600], TransitionTime = 0 };
    public static readonly HueLightState HabsWhite = new() { On = true, Bri = 254, Xy = [0.3227, 0.3290], TransitionTime = 0 };
    // Use hue/sat (not xy) so the bulb does its own gamut mapping — looks green on
    // any color bulb, instead of getting clipped to lime on gamut-B hardware.
    // hue 25500 ≈ pure green on the Hue 0..65535 wheel.
    public static readonly HueLightState GoalGreen = new() { On = true, Bri = 254, Hue = 25500, Sat = 254, TransitionTime = 0 };

    // Deliberately uninteresting beige for opponent goals. Low saturation, mid brightness.
    public static readonly HueLightState Beige     = new() { On = true, Bri = 140, Hue = 6000, Sat = 90, TransitionTime = 0 };

    public static readonly HueLightState Off       = new() { On = false, TransitionTime = 4 };
}
