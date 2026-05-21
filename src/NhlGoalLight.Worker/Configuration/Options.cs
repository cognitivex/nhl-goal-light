using System.ComponentModel.DataAnnotations;

namespace NhlGoalLight.Worker.Configuration;

public sealed class NhlOptions
{
    [Required]
    public string BaseUrl { get; init; } = "https://api-web.nhle.com/v1/";

    /// <summary>NHL team abbreviation, e.g. "MTL" for Montréal Canadiens.</summary>
    [Required, MinLength(2), MaxLength(3)]
    public string TeamAbbrev { get; init; } = "MTL";

    /// <summary>
    /// Sent on every NHL API request. Be a good citizen — identify yourself so
    /// the NHL ops team can contact you if your polling pattern is a problem.
    /// </summary>
    [Required]
    public string UserAgent { get; init; } = "NhlGoalLight/1.0";

    /// <summary>
    /// Time between landing polls. The NHL CDN caches landing for ~15-20s, so
    /// polling much faster than that just generates 304s — cheap but pointless.
    /// 10s is a reasonable balance: catches most refresh boundaries within ~5s
    /// while staying polite.
    /// </summary>
    [Range(3000, 60000)]
    public int PollIntervalMs { get; init; } = 10000;

    /// <summary>How many minutes before puck-drop to start polling.</summary>
    [Range(0, 240)]
    public int PreGameLeadMinutes { get; init; } = 5;
}

public sealed class HueOptions
{
    [Required]
    public string BridgeIp { get; init; } = "";

    /// <summary>
    /// The 40-character username returned by the bridge when you pair.
    /// See README for the pairing flow.
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

public sealed class AppOptions
{
    [Required]
    public string DataDirectory { get; init; } = "./data";

    /// <summary>Optional masochism: flash a deeply uninteresting beige when the opponent scores.</summary>
    public bool EnableOpponentGoalDimRed { get; init; } = false;

    /// <summary>
    /// What to do with the bulbs after the celebration ends.
    /// Null = turn them off. Otherwise restore to this state.
    /// </summary>
    public LightRestoreState? RestoreAfterSequence { get; init; }
}

public sealed class LightRestoreState
{
    public bool On { get; init; } = true;
    [Range(1, 254)] public int Brightness { get; init; } = 200;
    [Range(153, 500)] public int ColorTemp { get; init; } = 366;
}
