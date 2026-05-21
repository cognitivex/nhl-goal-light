using System.ComponentModel.DataAnnotations;

namespace NhlGoalLight.Nhl;

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
