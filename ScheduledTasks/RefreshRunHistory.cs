using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImdbRatings.ScheduledTasks;

/// <summary>Keeps the last five completed runs independently of editable plugin settings.</summary>
internal sealed class RefreshRunHistory
{
    // The scheduled task and API create separate instances that share the same file.
    private static readonly object FileLock = new();
    private readonly string _path;
    private readonly ILogger _logger;

    public RefreshRunHistory(string dataPath, ILogger logger)
    {
        _path = Path.Join(dataPath, "imdb-ratings", "run-history.json");
        _logger = logger;
    }

    public IReadOnlyList<RefreshRun> Read()
    {
        lock (FileLock)
        {
            return ReadFile();
        }
    }

    public void Append(RefreshRun run)
    {
        lock (FileLock)
        {
            IReadOnlyList<RefreshRun> previous;
            try
            {
                previous = ReadFile();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "IMDb ratings run history is corrupt; starting a new history");
                previous = Array.Empty<RefreshRun>();
            }

            var runs = new[] { run }.Concat(previous).Take(5).Select(Sanitize).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tempPath = _path + ".tmp";
            try
            {
                File.WriteAllText(tempPath, JsonSerializer.Serialize(runs));
                File.Move(tempPath, _path, overwrite: true);
            }
            finally
            {
                // A crash may leave this file behind; the next append overwrites it.
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Failed to remove temporary IMDb ratings run history");
                }
            }
        }
    }

    private IReadOnlyList<RefreshRun> ReadFile()
    {
        if (!File.Exists(_path))
        {
            return Array.Empty<RefreshRun>();
        }

        var runs = JsonSerializer.Deserialize<List<RefreshRun>>(File.ReadAllText(_path))
            ?? throw new JsonException("Expected a run history array.");
        return runs.Where(run => run is not null).Take(5).Select(Sanitize).ToArray();
    }

    private static RefreshRun Sanitize(RefreshRun run)
        => new()
        {
            StartedAtUtc = run.StartedAtUtc,
            FinishedAtUtc = run.FinishedAtUtc,
            Status = RefreshRun.LimitText(run.Status ?? "Unknown"),
            Summary = RefreshRun.LimitText(run.Summary ?? string.Empty),
            Warnings = (run.Warnings ?? new List<string>())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(RefreshRun.LimitText)
                .Distinct()
                .Take(5)
                .ToList()
        };
}
