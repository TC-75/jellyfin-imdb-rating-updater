using Jellyfin.Plugin.ImdbRatings.Configuration;
using Jellyfin.Plugin.ImdbRatings.Providers;

namespace Jellyfin.Plugin.ImdbRatings.Tests;

public class SeasonRatingCalculatorTests
{
    [Fact]
    public void CalculateSeasonAverages_BasicAverage_ReturnsCorrectValue()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, "tt0000002"),
            (seasonId, "tt0000003"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 1000),
            ["tt0000002"] = (8.0f, 2000),
            ["tt0000003"] = (9.0f, 3000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Single(result);
        Assert.Equal(8.0f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_ExcludesEpisodesWithNullImdbId()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, null),
            (seasonId, "tt0000002"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 1000),
            ["tt0000002"] = (9.0f, 2000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Single(result);
        Assert.Equal(8.0f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_ExcludesEpisodesWithEmptyImdbId()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, ""),
            (seasonId, "tt0000002"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 1000),
            ["tt0000002"] = (9.0f, 2000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Single(result);
        Assert.Equal(8.0f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_ExcludesEpisodesNotInRatingsDataset()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, "tt9999999"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.5f, 1000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Single(result);
        Assert.Equal(7.5f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_ExcludesEpisodesBelowMinimumVotes()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, "tt0000002"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 5000),
            ["tt0000002"] = (9.0f, 50),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 100);

        Assert.Single(result);
        Assert.Equal(7.0f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_IncludesEpisodeAtExactMinimumVotes()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, "tt0000002"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 100),
            ["tt0000002"] = (9.0f, 100),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 100);

        Assert.Single(result);
        Assert.Equal(8.0f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_OmitsSeasonWithNoEligibleEpisodes()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, null),
            (seasonId, "tt9999999"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>();

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculateSeasonAverages_OmitsSeasonWhenAllEpisodesBelowMinimumVotes()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, "tt0000002"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 10),
            ["tt0000002"] = (9.0f, 20),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 100);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculateSeasonAverages_HandleMultipleSeasons_IndependentAverages()
    {
        var season1 = Guid.NewGuid();
        var season2 = Guid.NewGuid();
        var season3 = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (season1, "tt0000001"),
            (season1, "tt0000002"),
            (season2, "tt0000003"),
            (season3, "tt0000004"),
            (season3, null),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 1000),
            ["tt0000002"] = (9.0f, 2000),
            ["tt0000003"] = (6.5f, 500),
            ["tt0000004"] = (8.0f, 3000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Equal(3, result.Count);
        Assert.Equal(8.0f, result[season1]);
        Assert.Equal(6.5f, result[season2]);
        Assert.Equal(8.0f, result[season3]);
    }

    [Fact]
    public void CalculateSeasonAverages_MultipleSeasons_OneWithNoEligibleEpisodes_IsOmitted()
    {
        var season1 = Guid.NewGuid();
        var season2 = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (season1, "tt0000001"),
            (season1, "tt0000002"),
            (season2, "tt9999999"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 1000),
            ["tt0000002"] = (9.0f, 2000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Single(result);
        Assert.Equal(8.0f, result[season1]);
        Assert.False(result.ContainsKey(season2));
    }

    [Fact]
    public void CalculateSeasonAverages_SingleEpisode_ReturnsEpisodeRating()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (8.5f, 5000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Single(result);
        Assert.Equal(8.5f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_EmptyInput_ReturnsEmptyResult()
    {
        var episodes = Array.Empty<(Guid, string?)>();
        var ratings = new Dictionary<string, (float Rating, int Votes)>();

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculateSeasonAverages_RoundsDownToOneDecimalPlace()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, "tt0000002"),
            (seasonId, "tt0000003"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 1000),
            ["tt0000002"] = (7.0f, 1000),
            ["tt0000003"] = (8.0f, 1000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        // (7+7+8)/3 = 7.333... → 7.3
        Assert.Equal(7.3f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_RoundsUpToOneDecimalPlace()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, "tt0000002"),
            (seasonId, "tt0000003"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 1000),
            ["tt0000002"] = (8.0f, 1000),
            ["tt0000003"] = (8.0f, 1000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        // (7+8+8)/3 = 7.666... → 7.7
        Assert.Equal(7.7f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_MidpointRounding_RoundsToEven()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, "tt0000002"),
            (seasonId, "tt0000003"),
            (seasonId, "tt0000004"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.0f, 1000),
            ["tt0000002"] = (7.0f, 1000),
            ["tt0000003"] = (7.0f, 1000),
            ["tt0000004"] = (8.0f, 1000),
        };

        var result = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        // (7+7+7+8)/4 = 7.25 → banker's rounding → 7.2
        Assert.Equal(7.2f, result[seasonId]);
    }

    [Fact]
    public void CalculateSeasonAverages_SameInputTwice_ProducesSameResult()
    {
        var seasonId = Guid.NewGuid();
        var episodes = new (Guid, string?)[]
        {
            (seasonId, "tt0000001"),
            (seasonId, "tt0000002"),
            (seasonId, "tt0000003"),
        };
        var ratings = new Dictionary<string, (float Rating, int Votes)>
        {
            ["tt0000001"] = (7.3f, 1000),
            ["tt0000002"] = (8.7f, 2000),
            ["tt0000003"] = (6.1f, 500),
        };

        var result1 = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);
        var result2 = SeasonRatingCalculator.CalculateSeasonAverages(episodes, ratings, minimumVotes: 1);

        Assert.Equal(result1[seasonId], result2[seasonId]);
    }

    [Fact]
    public void PluginConfiguration_IncludeSeasonAverages_DefaultsFalse()
    {
        var config = new PluginConfiguration();

        Assert.False(config.IncludeSeasonAverages);
    }

    [Fact]
    public void PluginConfiguration_IncludeSeries_DefaultsTrue()
    {
        var config = new PluginConfiguration();

        Assert.True(config.IncludeSeries);
    }

    [Fact]
    public void PluginConfiguration_MinimumVotes_DefaultsToOne()
    {
        var config = new PluginConfiguration();

        Assert.Equal(1, config.MinimumVotes);
    }
}
