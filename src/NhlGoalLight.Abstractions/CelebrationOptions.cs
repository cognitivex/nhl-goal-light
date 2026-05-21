using System.ComponentModel.DataAnnotations;

namespace NhlGoalLight.Abstractions;

/// <summary>
/// Team-agnostic celebration config. Every <see cref="IGoalLight"/> implementation
/// reads the same options so the worker is genuinely "pick any NHL team" without
/// recompiling. Defaults to the Montréal Canadiens' bleu-blanc-rouge.
/// </summary>
public sealed class CelebrationOptions
{
    /// <summary>Display name used in goal log lines, e.g. "Canadiens", "Maple Leafs".</summary>
    [Required]
    public string TeamName { get; init; } = "Canadiens";

    /// <summary>
    /// Hex colors cycled through during the celebration's color phase
    /// (e.g. "#AF1E2D"). The first entry is also held as the final solid color.
    /// At least one entry required.
    /// </summary>
    [Required, MinLength(1)]
    public string[] Colors { get; init; } = ["#AF1E2D", "#FFFFFF", "#192168"];

    /// <summary>
    /// Hex color flashed during the opening "a goal happened" phase before the
    /// team colors kick in. Defaults to green — universally readable and works
    /// on every color bulb regardless of gamut.
    /// </summary>
    [Required]
    public string GoalSignalColor { get; init; } = "#00FF00";

    /// <summary>Log line printed when a home-team goal sequence starts.</summary>
    [Required]
    public string GoalChant { get; init; } = "🚨 GOAAAAAL!";
}
