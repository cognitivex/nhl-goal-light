using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NhlGoalLight.Worker.Configuration;
using NhlGoalLight.Worker.Models;

namespace NhlGoalLight.Worker.Services;

public sealed class NhlClient(
    HttpClient http,
    IOptions<NhlOptions> options,
    ILogger<NhlClient> logger)
{
    private readonly NhlOptions _opts = options.Value;
    private static readonly TimeZoneInfo EasternTime = ResolveEasternTime();

    // Per-path ETag cache so subsequent polls can do conditional GETs.
    // Cloudflare caches landing for ~15-20s; polls inside that window 304 cheaply.
    private readonly ConcurrentDictionary<string, string> _etags = new();

    private static TimeZoneInfo ResolveEasternTime()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    }
    public async Task<ScheduleResponse?> GetScheduleAsync(DateOnly date, CancellationToken ct)
    {
        var path = $"schedule/{date:yyyy-MM-dd}";
        logger.LogDebug("GET {Path}", path);
        return await http.GetFromJsonAsync<ScheduleResponse>(path, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches the landing payload for a game. Returns null on 304 Not Modified —
    /// callers should treat null as "no change since last poll" and continue.
    /// </summary>
    public Task<LandingResponse?> GetLandingAsync(long gameId, CancellationToken ct)
        => GetConditionalAsync<LandingResponse>($"gamecenter/{gameId}/landing", ct);

    private async Task<T?> GetConditionalAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (_etags.TryGetValue(path, out var etag))
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            logger.LogTrace("304 Not Modified for {Path}", path);
            return null;
        }

        response.EnsureSuccessStatusCode();

        if (response.Headers.ETag is { } newEtag)
            _etags[path] = newEtag.ToString();

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the next un-ended game for the configured team, or null if none
    /// is scheduled in the API's current week window.
    /// </summary>
    public async Task<ScheduleGame?> FindNextTeamGameAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternTime));
        var schedule = await GetScheduleAsync(today, ct).ConfigureAwait(false);
        if (schedule is null) return null;

        var team = _opts.TeamAbbrev;
        return schedule.GameWeek
            .SelectMany(d => d.Games)
            .Where(g => g.HomeTeam.Abbrev == team || g.AwayTeam.Abbrev == team)
            .Where(g => !NhlGameState.IsEnded(g.GameState))
            .OrderBy(g => g.StartTimeUtc)
            .FirstOrDefault();
    }
}
