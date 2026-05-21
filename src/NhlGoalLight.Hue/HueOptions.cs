using System.ComponentModel.DataAnnotations;

namespace NhlGoalLight.Hue;

public sealed class HueOptions
{
    [Required]
    public string BridgeIp { get; init; } = "";

    /// <summary>
    /// The 40-character username returned by the bridge when you pair.
    /// See docs/setup-philips-hue.md for the pairing flow.
    /// </summary>
    [Required, MinLength(20)]
    public string ApiUser { get; init; } = "";

    /// <summary>IDs of the bulbs to flash. Get them via GET /api/{user}/lights.</summary>
    [Required, MinLength(1)]
    public int[] LightIds { get; init; } = [];

    [Range(50, 5000)]
    public int FlashIntervalMs { get; init; } = 400;

    [Range(1, 20)]
    public int FlashCycles { get; init; } = 4;

    [Range(0, 60000)]
    public int HoldFinalColorMs { get; init; } = 15000;
}
