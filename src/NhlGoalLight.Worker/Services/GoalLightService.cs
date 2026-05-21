using Microsoft.Extensions.Options;
using NhlGoalLight.Abstractions;
using NhlGoalLight.Nhl;
using NhlGoalLight.Worker.State;

namespace NhlGoalLight.Worker.Services;

public sealed class GoalLightService(
    NhlClient nhl,
    IGoalLight goalLight,
    PlayStateStore stateStore,
    GoalDetector detector,
    IOptions<NhlOptions> nhlOpts,
    ILogger<GoalLightService> logger) : BackgroundService
{
    private readonly NhlOptions _opts = nhlOpts.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("GoalLightService started. Watching {Team}.", _opts.TeamAbbrev);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var game = await nhl.FindNextTeamGameAsync(stoppingToken).ConfigureAwait(false);
                if (game is null)
                {
                    logger.LogInformation("No upcoming {Team} game in API window. Sleeping 1h.", _opts.TeamAbbrev);
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await WaitForGameWindowAsync(game, stoppingToken).ConfigureAwait(false);
                await PollGameAsync(game, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Main loop error. Retrying in 60s.");
                try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        logger.LogInformation("GoalLightService stopping.");
    }

    private async Task WaitForGameWindowAsync(ScheduleGame game, CancellationToken ct)
    {
        var lead = TimeSpan.FromMinutes(_opts.PreGameLeadMinutes);
        var openAt = game.StartTimeUtc - lead;
        var now = DateTimeOffset.UtcNow;
        if (now < openAt)
        {
            var wait = openAt - now;
            logger.LogInformation("{Team} game {GameId} at {Start:u}. Sleeping {Wait}.",
                _opts.TeamAbbrev, game.Id, game.StartTimeUtc, wait);
            await Task.Delay(wait, ct).ConfigureAwait(false);
        }
        else
        {
            logger.LogInformation("Game {GameId} window already open.", game.Id);
        }
    }

    private async Task PollGameAsync(ScheduleGame game, CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(_opts.PollIntervalMs);

        // Resume in-progress state from disk if it's for this same game, otherwise start fresh.
        var existing = await stateStore.LoadAsync(ct).ConfigureAwait(false);
        var state = existing?.GameId == game.Id
            ? existing
            : new PlayState(game.Id, HomeScore: 0, AwayScore: 0, Initialized: false);

        logger.LogInformation("Polling game {GameId} every {Interval} (resumed={Resumed}).",
            game.Id, interval, existing?.GameId == game.Id);

        while (!ct.IsCancellationRequested)
        {
            LandingResponse? landing;
            try
            {
                landing = await nhl.GetLandingAsync(game.Id, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Landing fetch failed; retrying after interval.");
                await Task.Delay(interval, ct).ConfigureAwait(false);
                continue;
            }

            if (landing is not null)
            {
                state = await ProcessScoreAsync(landing, state, ct).ConfigureAwait(false);

                if (NhlGameState.IsEnded(landing.GameState))
                {
                    logger.LogInformation("Game {GameId} ended ({State}).", game.Id, landing.GameState);
                    return;
                }
            }

            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    private async Task<PlayState> ProcessScoreAsync(LandingResponse landing, PlayState state, CancellationToken ct)
    {
        var result = detector.Detect(landing, state, _opts.TeamAbbrev);

        if (result.TeamNotInGame)
        {
            logger.LogWarning("Game {GameId} doesn't involve {Team}; bailing.", landing.Id, _opts.TeamAbbrev);
            return state;
        }

        if (result.JustInitialized)
        {
            logger.LogInformation(
                "Game {GameId} initialized; baselining at {Home}-{Away} ({State}).",
                landing.Id, landing.HomeTeam.Score, landing.AwayTeam.Score, landing.GameState);
            await stateStore.SaveAsync(result.NewState, ct).ConfigureAwait(false);
            return result.NewState;
        }

        FireGoalLight(result.HomeGoal, ct);
        FireGoalLight(result.AwayGoal, ct);

        if (result.NewState != state)
        {
            await stateStore.SaveAsync(result.NewState, ct).ConfigureAwait(false);
            return result.NewState;
        }

        return state;
    }

    private void FireGoalLight(GoalEvent? goal, CancellationToken ct)
    {
        if (goal is null) return;
        logger.LogInformation("Goal: {Abbrev} {Old}→{New}.", goal.Abbrev, goal.OldScore, goal.NewScore);
        // Fire-and-forget so polling isn't blocked on the 20s+ light show.
        // The IGoalLight implementation has its own re-entrancy guard.
        if (goal.IsOurTeam) _ = Task.Run(() => goalLight.PlayOurGoalAsync(ct), ct);
        else                _ = Task.Run(() => goalLight.PlayOpponentGoalAsync(ct), ct);
    }
}
