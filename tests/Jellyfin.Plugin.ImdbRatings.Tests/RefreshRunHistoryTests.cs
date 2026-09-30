using System.Text.Json;
using Jellyfin.Plugin.ImdbRatings.ScheduledTasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ImdbRatings.Tests;

public class RefreshRunHistoryTests
{
    [Fact]
    public void Append_RotatesToFiveNewestAndSurvivesNewInstances()
    {
        using var temp = new TempDirectory();
        Assert.Empty(CreateHistory(temp).Read());

        for (var i = 0; i < 8; i++)
        {
            CreateHistory(temp).Append(new RefreshRun { Summary = $"Run {i}" });
        }

        Assert.Equal(new[] { "Run 7", "Run 6", "Run 5", "Run 4", "Run 3" },
            CreateHistory(temp).Read().Select(run => run.Summary));
        var files = Directory.GetFiles(temp.Path, "*", SearchOption.AllDirectories);
        Assert.EndsWith("run-history.json", Assert.Single(files));
        Assert.Equal(5, JsonDocument.Parse(File.ReadAllText(files[0])).RootElement.GetArrayLength());
    }

    [Fact]
    public void Append_CorruptHistoryAndLeftoverTemporaryFile_RecoverOnNextRun()
    {
        using var temp = new TempDirectory();
        var directory = Directory.CreateDirectory(temp.PathFor("imdb-ratings")).FullName;
        File.WriteAllText(Path.Join(directory, "run-history.json"), "{truncated");
        File.WriteAllText(Path.Join(directory, "run-history.json.tmp"), "interrupted write");

        CreateHistory(temp).Append(new RefreshRun { Summary = "Recovered" });

        Assert.Equal("Recovered", Assert.Single(CreateHistory(temp).Read()).Summary);
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void Append_WriteFails_LeavesPreviousHistoryIntact()
    {
        using var temp = new TempDirectory();
        var history = CreateHistory(temp);
        history.Append(new RefreshRun { Summary = "Previous run" });
        Directory.CreateDirectory(temp.PathFor("imdb-ratings/run-history.json.tmp"));

        Assert.ThrowsAny<IOException>(() =>
        {
            try
            {
                history.Append(new RefreshRun { Summary = "New run" });
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException("Temporary path is a directory.", ex);
            }
        });

        Assert.Equal("Previous run", Assert.Single(history.Read()).Summary);
    }

    [Fact]
    public void Append_BoundsMessagesAndRemovesUrlsFromDiskAndResponse()
    {
        using var temp = new TempDirectory();
        var run = new RefreshRun
        {
            Summary = "Failed GET https://example.invalid/ratings.gz?key=test " + new string('x', 2000),
            Warnings = Enumerable.Range(0, 20)
                .Select(i => $"Attempt {i}: HTTP://example.invalid/ratings.gz " + new string('y', 2000))
                .ToList()
        };

        CreateHistory(temp).Append(run);

        var saved = Assert.Single(CreateHistory(temp).Read());
        Assert.Equal(500, saved.Summary.Length);
        Assert.Equal(5, saved.Warnings.Count);
        Assert.All(saved.Warnings, message => Assert.Equal(500, message.Length));
        var json = File.ReadAllText(temp.PathFor("imdb-ratings/run-history.json"));
        Assert.DoesNotContain("example.invalid", json);
        Assert.DoesNotContain("key=test", json);
    }

    [Fact]
    public async Task Append_ConcurrentInstances_DoNotLoseEntries()
    {
        using var temp = new TempDirectory();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(i => Task.Run(() =>
            CreateHistory(temp).Append(new RefreshRun { Summary = $"Run {i}" }))));

        Assert.Equal(5, CreateHistory(temp).Read().Select(run => run.Summary).Distinct().Count());
    }

    private static RefreshRunHistory CreateHistory(TempDirectory temp)
        => new(temp.Path, NullLogger.Instance);
}
