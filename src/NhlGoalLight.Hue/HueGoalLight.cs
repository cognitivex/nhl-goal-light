using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NhlGoalLight.Abstractions;

namespace NhlGoalLight.Hue;

public sealed class HueGoalLight : IGoalLight
{
    private readonly HueClient _hue;
    private readonly HueOptions _hueOpts;
    private readonly AppOptions _appOpts;
    private readonly CelebrationOptions _celebration;
    private readonly ILogger<HueGoalLight> _logger;

    // Precomputed at construction so we don't re-parse hex on every flash.
    private readonly HueLightState _goalSignal;
    private readonly HueLightState[] _teamColors;

    // Re-entrancy guard. If a sequence is already running (rare: two goals
    // surfaced in adjacent polls), skip the new trigger rather than overlap.
    private int _running;

    public HueGoalLight(
        HueClient hue,
        IOptions<HueOptions> hueOpts,
        IOptions<AppOptions> appOpts,
        IOptions<CelebrationOptions> celebrationOpts,
        ILogger<HueGoalLight> logger)
    {
        _hue = hue;
        _hueOpts = hueOpts.Value;
        _appOpts = appOpts.Value;
        _celebration = celebrationOpts.Value;
        _logger = logger;

        _goalSignal = HueColor.FromHex(_celebration.GoalSignalColor);
        _teamColors = _celebration.Colors.Select(HueColor.FromHex).ToArray();
    }

    public async Task PlayOurGoalAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            _logger.LogInformation("Sequence already running; skipping new goal trigger.");
            return;
        }
        try
        {
            _logger.LogInformation("{Chant} {Team} scored — flashing signal, then team colors.",
                _celebration.GoalChant, _celebration.TeamName);
            var phase = TimeSpan.FromSeconds(5);
            var interval = TimeSpan.FromMilliseconds(_hueOpts.FlashIntervalMs);

            // Phase 1: flashing goal-signal color (on ↔ off) for 5 seconds.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var on = true;
            while (sw.Elapsed < phase)
            {
                await _hue.SetAllAsync(on ? _goalSignal : HueLightState.Off, ct).ConfigureAwait(false);
                await Task.Delay(interval, ct).ConfigureAwait(false);
                on = !on;
            }

            // Phase 2: rotate through team colors for another 5 seconds.
            sw.Restart();
            var step = 0;
            while (sw.Elapsed < phase)
            {
                await _hue.SetAllAsync(_teamColors[step % _teamColors.Length], ct).ConfigureAwait(false);
                await Task.Delay(interval, ct).ConfigureAwait(false);
                step++;
            }

            // Hold the first (primary) team color, then restore.
            await _hue.SetAllAsync(_teamColors[0], ct).ConfigureAwait(false);
            await Task.Delay(_hueOpts.HoldFinalColorMs, ct).ConfigureAwait(false);
            await RestoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown mid-sequence; ignore.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Goal sequence failed.");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    public async Task PlayOpponentGoalAsync(CancellationToken ct)
    {
        if (!_appOpts.EnableOpponentGoalDimRed) return;
        if (Interlocked.Exchange(ref _running, 1) == 1) return;
        try
        {
            _logger.LogInformation("Opponent scored. Flashing a profoundly uninteresting beige.");
            var interval = TimeSpan.FromMilliseconds(_hueOpts.FlashIntervalMs);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var on = true;
            while (sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                await _hue.SetAllAsync(on ? HueLightState.Beige : HueLightState.Off, ct).ConfigureAwait(false);
                await Task.Delay(interval, ct).ConfigureAwait(false);
                on = !on;
            }
            await RestoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Opponent goal sequence failed.");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private Task RestoreAsync(CancellationToken ct)
    {
        var restore = _appOpts.RestoreAfterSequence;
        var state = restore is null
            ? HueLightState.Off
            : new HueLightState
            {
                On = restore.On,
                Bri = restore.Brightness,
                Ct = restore.ColorTemp,
                TransitionTime = 10,
            };
        return _hue.SetAllAsync(state, ct);
    }
}
