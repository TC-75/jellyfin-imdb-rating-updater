using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ImdbRatings.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImdbRatings.Providers;

/// <summary>
/// Applies IMDb community ratings at library-scan time, so newly added items are rated immediately
/// rather than waiting for the next scheduled refresh.
/// </summary>
/// <remarks>
/// This is deliberately an <see cref="ICustomMetadataProvider{TItemType}"/> rather than an
/// <see cref="IRemoteMetadataProvider{TItemType, TLookupInfo}"/>, for two reasons that are both fatal to the
/// remote-provider approach:
///
/// <para>
/// Ordering. Jellyfin propagates newly discovered provider IDs to the lookup info only *after* each remote
/// provider returns, and merges community ratings on a first-non-null-wins basis. A remote provider ordered
/// before TMDb therefore has no IMDb ID to look up on a normally identified new item, while one ordered after
/// TMDb has the ID but finds its rating discarded because TMDb already supplied one. Custom providers run
/// after the whole remote merge completes and mutate the item directly, so neither problem applies.
/// </para>
///
/// <para>
/// Enablement. <c>ProviderManager.CanRefreshMetadata</c> only consults the library's <c>MetadataFetchers</c>
/// allowlist for <see cref="IRemoteMetadataProvider"/>s. A newly introduced fetcher name is absent from every
/// existing library's saved list, so a remote provider would silently never run. Custom providers bypass that
/// check, which makes the plugin's own toggle the single source of truth.
/// </para>
///
/// Episodes are deliberately not handled here. Series-level IMDb ratings and vote counts are sufficient for
/// collection and filtering purposes, while episode ratings are only queried by the scheduled task when needed
/// to calculate optional season averages.
/// </remarks>
public class ImdbRatingsItemProvider :
    ICustomMetadataProvider<Movie>,
    ICustomMetadataProvider<Series>
{
    private readonly ImdbRatingsIndexCache _indexCache;
    private readonly ILogger<ImdbRatingsItemProvider> _logger;

    public ImdbRatingsItemProvider(
        IApplicationPaths applicationPaths,
        ILogger<ImdbRatingsItemProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(applicationPaths);

        _logger = logger;
        _indexCache = ImdbRatingsIndexCache.GetShared(
            ImdbRatingsIndex.GetIndexPath(applicationPaths.DataPath),
            logger);
    }

    /// <inheritdoc />
    public string Name => "IMDb Ratings";

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Movie item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        => ApplyRatingAsync(item, config => config.IncludeMovies, cancellationToken);

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Series item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        => ApplyRatingAsync(item, config => config.IncludeSeries, cancellationToken);

    private async Task<ItemUpdateType> ApplyRatingAsync(
        BaseItem item,
        Func<PluginConfiguration, bool> isTypeEnabled,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return ItemUpdateType.None;
        }

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        if (!config.EnableMetadataProvider)
        {
            // Release the shared index if the setting was turned off while the server was running.
            _indexCache.Invalidate();
            return ItemUpdateType.None;
        }

        if (!isTypeEnabled(config))
        {
            return ItemUpdateType.None;
        }

        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            return ItemUpdateType.None;
        }

        var index = await _indexCache.GetIndexAsync(cancellationToken).ConfigureAwait(false);
        if (index is null)
        {
            return ItemUpdateType.None;
        }

        if (!index.TryGetRating(imdbId, config.MinimumVotes, out var rating, out var votes))
        {
            return ItemUpdateType.None;
        }

        // Match the scheduled task's tolerance so the two paths agree on what counts as a change.
        var ratingUnchanged =
            item.CommunityRating.HasValue
            && Math.Abs(item.CommunityRating.Value - rating) < 0.01f;

        var votesChanged = HasMeaningfulVoteChange(item.CustomRating, votes);

        if (ratingUnchanged && !votesChanged)
        {
            return ItemUpdateType.None;
        }

        if (config.EnableItemDebugLogging && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Applying IMDb rating {Rating} to CommunityRating and vote count {Votes} to CustomRating={UpdateVotes} for \"{Name}\" ({ImdbId}) at scan time",
                rating,
                votes,
                votesChanged,
                item.Name,
                imdbId);
        }

        item.CommunityRating = rating;
        if (votesChanged)
        {
            item.CustomRating = votes.ToString(CultureInfo.InvariantCulture);
        }

        return ItemUpdateType.MetadataDownload;
    }

    private static bool HasMeaningfulVoteChange(string? currentCustomRating, int newVotes)
    {
        if (!int.TryParse(currentCustomRating, NumberStyles.None, CultureInfo.InvariantCulture, out var oldVotes)
            || oldVotes <= 0)
        {
            return true;
        }

        var voteDifference = Math.Abs((long)newVotes - oldVotes);
        var percentageDifference = (double)voteDifference / oldVotes;

        return voteDifference >= 20 && percentageDifference >= 0.05;
    }
}
