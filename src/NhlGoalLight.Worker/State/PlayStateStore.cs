using System.Text.Json;
using Microsoft.Extensions.Options;
using NhlGoalLight.Abstractions;

namespace NhlGoalLight.Worker.State;

/// <summary>
/// Tracks the last play we've seen so a container restart mid-game doesn't
/// re-fire goals on the next poll. Single-writer, single-reader: serialize
/// via a semaphore.
/// </summary>
public sealed class PlayStateStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public PlayStateStore(IOptions<AppOptions> opts)
    {
        Directory.CreateDirectory(opts.Value.DataDirectory);
        _path = Path.Combine(opts.Value.DataDirectory, "play-state.json");
    }

    public async Task<PlayState?> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return null;
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<PlayState>(stream, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(PlayState state, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var tmp = _path + ".tmp";
            await using (var stream = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(stream, state, cancellationToken: ct)
                    .ConfigureAwait(false);
            }
            File.Move(tmp, _path, overwrite: true);
        }
        finally { _lock.Release(); }
    }
}

/// <param name="GameId">NHL game ID this state belongs to.</param>
/// <param name="HomeScore">Home team score as of the last poll.</param>
/// <param name="AwayScore">Away team score as of the last poll.</param>
/// <param name="Initialized">
/// False until the first poll completes. Used to distinguish "service just
/// started, game already in progress" (baseline current scores, don't
/// retroactively celebrate) from "started pre-game" (baseline at 0-0).
/// </param>
public sealed record PlayState(long GameId, int HomeScore, int AwayScore, bool Initialized = false);
