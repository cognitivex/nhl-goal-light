namespace NhlGoalLight.Abstractions;

/// <summary>
/// The contract a smart-light integration must satisfy to be drivable by the
/// goal-watcher. One implementation per vendor (Hue, LIFX, WLED, Govee, ...).
/// Implementations own their own colors, choreography, and re-entrancy.
/// </summary>
public interface IGoalLight
{
    Task PlayOurGoalAsync(CancellationToken ct);
    Task PlayOpponentGoalAsync(CancellationToken ct);
}