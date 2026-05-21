using System.Globalization;

namespace NhlGoalLight.Hue;

/// <summary>
/// Converts user-supplied hex colors ("#AF1E2D") into <see cref="HueLightState"/>
/// payloads. Uses hue/saturation (not CIE xy) so each bulb does its own gamut
/// mapping — looks correct on gamut-B, -C, and white-ambiance bulbs alike,
/// instead of clipping to the wrong shade on the wrong hardware.
/// </summary>
public static class HueColor
{
    public static HueLightState FromHex(string hex)
    {
        var (r, g, b) = ParseHex(hex);

        // White / near-white: bypass the hue wheel, use color temperature region.
        // Hue API hue/sat with sat=0 still produces a tinted white on some bulbs;
        // explicit xy at D65 is more reliable.
        if (r == g && g == b)
        {
            return new HueLightState
            {
                On = true,
                Bri = 254,
                Xy = [0.3227, 0.3290], // D65 white
                TransitionTime = 0,
            };
        }

        var (hue, sat) = RgbToHueSat(r, g, b);
        return new HueLightState
        {
            On = true,
            Bri = 254,
            Hue = hue,
            Sat = sat,
            TransitionTime = 0,
        };
    }

    private static (byte R, byte G, byte B) ParseHex(string hex)
    {
        var span = hex.AsSpan().TrimStart('#');
        if (span.Length != 6)
            throw new ArgumentException($"Hex color must be 6 hex digits (got '{hex}').", nameof(hex));

        var r = byte.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = byte.Parse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = byte.Parse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (r, g, b);
    }

    // Standard HSV conversion. Hue API uses hue 0..65535 (full circle) and
    // saturation 0..254. We keep value at max (Bri=254 set separately) — picking
    // a color is independent of brightness on the bulb side.
    private static (int Hue, int Sat) RgbToHueSat(byte r, byte g, byte b)
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        double h;
        if (delta == 0)
            h = 0;
        else if (max == rf)
            h = 60 * (((gf - bf) / delta) % 6);
        else if (max == gf)
            h = 60 * (((bf - rf) / delta) + 2);
        else
            h = 60 * (((rf - gf) / delta) + 4);
        if (h < 0) h += 360;

        var s = max == 0 ? 0 : delta / max;

        var hueScaled = (int)Math.Round(h / 360.0 * 65535) % 65536;
        var satScaled = (int)Math.Round(s * 254);
        return (hueScaled, satScaled);
    }
}
