using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.ImdbRatings.Providers;

public static class SeasonRatingCalculator
{
    public static Dictionary<Guid, float> CalculateSeasonAverages(
        IEnumerable<(Guid SeasonId, string? ImdbId)> episodes,
        IReadOnlyDictionary<string, (float Rating, int Votes)> ratings,
        int minimumVotes)
    {
        var result = new Dictionary<Guid, float>();

        foreach (var group in episodes.GroupBy(e => e.SeasonId))
        {
            float sum = 0;
            int count = 0;

            foreach (var (_, imdbId) in group)
            {
                if (!string.IsNullOrEmpty(imdbId)
                    && ratings.TryGetValue(imdbId, out var ratingData)
                    && ratingData.Votes >= minimumVotes)
                {
                    sum += ratingData.Rating;
                    count++;
                }
            }

            if (count > 0)
            {
                result[group.Key] = MathF.Round(sum / count, 1);
            }
        }

        return result;
    }
}
