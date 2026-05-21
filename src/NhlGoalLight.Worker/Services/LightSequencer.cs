using Microsoft.Extensions.Options;
using NhlGoalLight.Worker.Configuration;

namespace NhlGoalLight.Worker.Services;

public sealed class LightSequencer(
    HueClient hue,
    IOptions<HueOptions> hueOpts,
    IOptions<AppOptions> appOpts,
    ILogger<LightSequencer> logger)
{
    private readonly HueOptions _hueOpts = hueOpts.Value;
    private readonly AppOptions _appOpts = appOpts.Value;

    // Re-entrancy guard. If a sequence is already running (rare: two goals
    // surfaced in adjacent polls), skip the new trigger rather than overlap.
    private int _running;

    public async Task PlayHabsGoalAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            logger.LogInformation("Sequence already running; skipping new goal trigger.");
            return;
        }
        try
        {
            logger.LogInformation("🚨 ET LE BUT! Flashing green, then rotating bleu-blanc-rouge.");
            var phase = TimeSpan.FromSeconds(5);
            var interval = TimeSpan.FromMilliseconds(_hueOpts.FlashIntervalMs);

            // Phase 1: flashing green (green ↔ off) for 5 seconds.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var green = true;
            while (sw.Elapsed < phase)
            {
                await hue.SetAllAsync(green ? HueLightState.GoalGreen : HueLightState.Off, ct).ConfigureAwait(false);
                await Task.Delay(interval, ct).ConfigureAwait(false);
                green = !green;
            }

            // Phase 2: rotate red → white → blue for another 5 seconds.
            var cycle = new[] { HueLightState.HabsRed, HueLightState.HabsWhite, HueLightState.HabsBlue };
            sw.Restart();
            var step = 0;
            while (sw.Elapsed < phase)
            {
                await hue.SetAllAsync(cycle[step % cycle.Length], ct).ConfigureAwait(false);
                await Task.Delay(interval, ct).ConfigureAwait(false);
                step++;
            }

            await hue.SetAllAsync(HueLightState.HabsRed, ct).ConfigureAwait(false);
            await Task.Delay(_hueOpts.HoldFinalColorMs, ct).ConfigureAwait(false);
            await RestoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown mid-sequence; ignore.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Goal sequence failed.");
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
            logger.LogInformation("Opponent scored. Flashing a profoundly uninteresting beige.");
            var interval = TimeSpan.FromMilliseconds(_hueOpts.FlashIntervalMs);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var on = true;
            while (sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                await hue.SetAllAsync(on ? HueLightState.Beige : HueLightState.Off, ct).ConfigureAwait(false);
                await Task.Delay(interval, ct).ConfigureAwait(false);
                on = !on;
            }
            await RestoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Opponent goal sequence failed.");
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
        return hue.SetAllAsync(state, ct);
    }
}
