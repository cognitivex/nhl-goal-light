using NhlGoalLight.Nhl;
using NhlGoalLight.Worker.Services;
using NhlGoalLight.Worker.State;

namespace NhlGoalLight.Worker.Tests;

public class GoalDetectorTests
{
    private const string OurTeam = "MTL";
    private const string OtherTeam = "TOR";
    private const string ThirdTeam = "BOS";
    private const long GameId = 2024020001;

    private readonly GoalDetector _detector = new();

    private static LandingResponse Landing(string homeAbbrev, int homeScore, string awayAbbrev, int awayScore)
        => new(
            Id: GameId,
            GameState: NhlGameState.Live,
            HomeTeam: new LandingTeam(Id: 1, Abbrev: homeAbbrev, Score: homeScore),
            AwayTeam: new LandingTeam(Id: 2, Abbrev: awayAbbrev, Score: awayScore));

    [Fact]
    public void TeamNotInGame_returns_unchanged_state_with_flag()
    {
        var state = new PlayState(GameId, HomeScore: 0, AwayScore: 0, Initialized: true);
        var landing = Landing(OtherTeam, 1, ThirdTeam, 2);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.True(result.TeamNotInGame);
        Assert.False(result.JustInitialized);
        Assert.Null(result.HomeGoal);
        Assert.Null(result.AwayGoal);
        Assert.Equal(state, result.NewState);
    }

    [Fact]
    public void FirstContact_baselines_current_scores_and_does_not_fire_goal()
    {
        // Service started after game began — scores already 2-1; we must not
        // retroactively celebrate the goals we missed.
        var state = new PlayState(GameId, HomeScore: 0, AwayScore: 0, Initialized: false);
        var landing = Landing(OurTeam, 2, OtherTeam, 1);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.True(result.JustInitialized);
        Assert.False(result.TeamNotInGame);
        Assert.Null(result.HomeGoal);
        Assert.Null(result.AwayGoal);
        Assert.Equal(new PlayState(GameId, 2, 1, Initialized: true), result.NewState);
    }

    [Fact]
    public void OurGoal_when_we_are_home()
    {
        var state = new PlayState(GameId, HomeScore: 1, AwayScore: 0, Initialized: true);
        var landing = Landing(OurTeam, 2, OtherTeam, 0);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.NotNull(result.HomeGoal);
        Assert.True(result.HomeGoal!.IsOurTeam);
        Assert.Equal(OurTeam, result.HomeGoal.Abbrev);
        Assert.Equal(1, result.HomeGoal.OldScore);
        Assert.Equal(2, result.HomeGoal.NewScore);
        Assert.Null(result.AwayGoal);
        Assert.Equal(new PlayState(GameId, 2, 0, Initialized: true), result.NewState);
    }

    [Fact]
    public void OurGoal_when_we_are_away()
    {
        var state = new PlayState(GameId, HomeScore: 0, AwayScore: 1, Initialized: true);
        var landing = Landing(OtherTeam, 0, OurTeam, 2);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.NotNull(result.AwayGoal);
        Assert.True(result.AwayGoal!.IsOurTeam);
        Assert.Equal(OurTeam, result.AwayGoal.Abbrev);
        Assert.Equal(1, result.AwayGoal.OldScore);
        Assert.Equal(2, result.AwayGoal.NewScore);
        Assert.Null(result.HomeGoal);
    }

    [Fact]
    public void OpponentGoal_when_we_are_home()
    {
        var state = new PlayState(GameId, HomeScore: 0, AwayScore: 0, Initialized: true);
        var landing = Landing(OurTeam, 0, OtherTeam, 1);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.NotNull(result.AwayGoal);
        Assert.False(result.AwayGoal!.IsOurTeam);
        Assert.Equal(OtherTeam, result.AwayGoal.Abbrev);
        Assert.Null(result.HomeGoal);
    }

    [Fact]
    public void OpponentGoal_when_we_are_away()
    {
        var state = new PlayState(GameId, HomeScore: 0, AwayScore: 0, Initialized: true);
        var landing = Landing(OtherTeam, 1, OurTeam, 0);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.NotNull(result.HomeGoal);
        Assert.False(result.HomeGoal!.IsOurTeam);
        Assert.Equal(OtherTeam, result.HomeGoal.Abbrev);
        Assert.Null(result.AwayGoal);
    }

    [Fact]
    public void BothTeamsScoreInSamePoll_returns_both_goals()
    {
        // Rare but possible: two goals surfaced in the same poll interval.
        var state = new PlayState(GameId, HomeScore: 1, AwayScore: 1, Initialized: true);
        var landing = Landing(OurTeam, 2, OtherTeam, 2);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.NotNull(result.HomeGoal);
        Assert.True(result.HomeGoal!.IsOurTeam);
        Assert.NotNull(result.AwayGoal);
        Assert.False(result.AwayGoal!.IsOurTeam);
        Assert.Equal(new PlayState(GameId, 2, 2, Initialized: true), result.NewState);
    }

    [Fact]
    public void OverturnedGoal_rebaselines_without_firing()
    {
        // Goal waved off on review: score went from 2 down to 1. No goal event;
        // state should silently rebaseline to the new (lower) score.
        var state = new PlayState(GameId, HomeScore: 2, AwayScore: 0, Initialized: true);
        var landing = Landing(OurTeam, 1, OtherTeam, 0);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.Null(result.HomeGoal);
        Assert.Null(result.AwayGoal);
        Assert.False(result.JustInitialized);
        Assert.Equal(new PlayState(GameId, 1, 0, Initialized: true), result.NewState);
        Assert.NotEqual(state, result.NewState);
    }

    [Fact]
    public void NoChange_returns_same_state_instance_semantics()
    {
        var state = new PlayState(GameId, HomeScore: 2, AwayScore: 1, Initialized: true);
        var landing = Landing(OurTeam, 2, OtherTeam, 1);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.Null(result.HomeGoal);
        Assert.Null(result.AwayGoal);
        Assert.Equal(state, result.NewState);
    }

    [Fact]
    public void MultiGoalJump_in_single_poll_reports_old_and_new_scores()
    {
        // If we missed a poll and 0 → 2 shows up at once, the GoalEvent should
        // still carry the actual old/new scores (a single "goal" representing
        // the jump). The light fires once; this matches existing behavior.
        var state = new PlayState(GameId, HomeScore: 0, AwayScore: 0, Initialized: true);
        var landing = Landing(OurTeam, 2, OtherTeam, 0);

        var result = _detector.Detect(landing, state, OurTeam);

        Assert.NotNull(result.HomeGoal);
        Assert.Equal(0, result.HomeGoal!.OldScore);
        Assert.Equal(2, result.HomeGoal.NewScore);
    }
}
