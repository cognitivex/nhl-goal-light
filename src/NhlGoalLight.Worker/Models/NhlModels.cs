using System.Text.Json.Serialization;

namespace NhlGoalLight.Worker.Models;

// --- /v1/schedule/{date} ---

public sealed record ScheduleResponse(
    [property: JsonPropertyName("gameWeek")] IReadOnlyList<GameWeekDay> GameWeek
);

public sealed record GameWeekDay(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("games")] IReadOnlyList<ScheduleGame> Games
);

public sealed record ScheduleGame(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("startTimeUTC")] DateTimeOffset StartTimeUtc,
    [property: JsonPropertyName("gameState")] string GameState,
    [property: JsonPropertyName("homeTeam")] TeamRef HomeTeam,
    [property: JsonPropertyName("awayTeam")] TeamRef AwayTeam
);

public sealed record TeamRef(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("abbrev")] string Abbrev
);

// --- /v1/gamecenter/{id}/landing ---
//
// We use landing instead of play-by-play because it's ~14x smaller and
// commits score changes faster (the NHL backend updates landing/boxscore
// before filling in detailed play-by-play entries). Goals are detected via
// score deltas on awayTeam/homeTeam, not by iterating plays.

public sealed record LandingResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("gameState")] string GameState,
    [property: JsonPropertyName("homeTeam")] LandingTeam HomeTeam,
    [property: JsonPropertyName("awayTeam")] LandingTeam AwayTeam
);

public sealed record LandingTeam(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("abbrev")] string Abbrev,
    [property: JsonPropertyName("score")] int Score
);

/// <summary>
/// NHL game state values we care about. The API uses string codes:
/// FUT = future, PRE = pre-game, LIVE/CRIT = in progress, OFF/FINAL = done.
/// </summary>
public static class NhlGameState
{
    public const string Future = "FUT";
    public const string Pregame = "PRE";
    public const string Live = "LIVE";
    public const string Critical = "CRIT";
    public const string Off = "OFF";
    public const string Final = "FINAL";

    public static bool IsEnded(string state) => state is Off or Final;
    public static bool IsActive(string state) => state is Live or Critical or Pregame;
}
