using NhlGoalLight.Nhl;
using NhlGoalLight.Worker.State;

namespace NhlGoalLight.Worker.Services;

/// <summary>
/// Pure goal-detection logic: given a landing payload and the last known state,
/// decides whether a goal happened, who scored, and what the new state is.
/// No IO, no logging — keeps the side-effecting orchestration in
/// <see cref="GoalLightService"/> so this part stays unit-testable.
/// </summary>
public sealed class GoalDetector
{
    public GoalDetectionResult Detect(LandingResponse landing, PlayState state, string teamAbbrev)
    {
        var ourIsHome = landing.HomeTeam.Abbrev == teamAbbrev;
        var ourIsAway = landing.AwayTeam.Abbrev == teamAbbrev;

        if (!ourIsHome && !ourIsAway)
            return new GoalDetectionResult(state, null, null, TeamNotInGame: true);

        // First contact: baseline at the current scores so we don't retroactively
        // celebrate goals from before we started watching.
        if (!state.Initialized)
        {
            var initialized = state with
            {
                HomeScore = landing.HomeTeam.Score,
                AwayScore = landing.AwayTeam.Score,
                Initialized = true,
            };
            return new GoalDetectionResult(initialized, null, null, JustInitialized: true);
        }

        var homeDelta = landing.HomeTeam.Score - state.HomeScore;
        var awayDelta = landing.AwayTeam.Score - state.AwayScore;

        // Positive delta = goal. Negative delta = overturn on review; rebaseline silently.
        var homeGoal = homeDelta > 0
            ? new GoalEvent(landing.HomeTeam.Abbrev, state.HomeScore, landing.HomeTeam.Score, IsOurTeam: ourIsHome)
            : null;
        var awayGoal = awayDelta > 0
            ? new GoalEvent(landing.AwayTeam.Abbrev, state.AwayScore, landing.AwayTeam.Score, IsOurTeam: ourIsAway)
            : null;

        var newState = (homeDelta != 0 || awayDelta != 0)
            ? state with
            {
                HomeScore = landing.HomeTeam.Score,
                AwayScore = landing.AwayTeam.Score,
            }
            : state;

        return new GoalDetectionResult(newState, homeGoal, awayGoal);
    }
}

/// <param name="NewState">The state to persist (equal to the input state when nothing changed).</param>
/// <param name="HomeGoal">Non-null when the home team's score went up since the last poll.</param>
/// <param name="AwayGoal">Non-null when the away team's score went up since the last poll.</param>
/// <param name="TeamNotInGame">True when neither team in the landing matches the configured abbrev.</param>
/// <param name="JustInitialized">True on the first contact for a game (the new state holds the baseline).</param>
public sealed record GoalDetectionResult(
    PlayState NewState,
    GoalEvent? HomeGoal,
    GoalEvent? AwayGoal,
    bool TeamNotInGame = false,
    bool JustInitialized = false);

/// <param name="Abbrev">Scoring team's abbreviation (for logging).</param>
/// <param name="OldScore">Score before the goal.</param>
/// <param name="NewScore">Score after the goal.</param>
/// <param name="IsOurTeam">True if the scoring team is the configured team.</param>
public sealed record GoalEvent(string Abbrev, int OldScore, int NewScore, bool IsOurTeam);
