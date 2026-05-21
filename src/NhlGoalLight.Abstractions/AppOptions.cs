using System.ComponentModel.DataAnnotations;

namespace NhlGoalLight.Abstractions;

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