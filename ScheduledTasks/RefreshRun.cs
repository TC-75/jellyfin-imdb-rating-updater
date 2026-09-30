using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ImdbRatings.ScheduledTasks;

/// <summary>A small, persistent summary of one ratings refresh.</summary>
public sealed class RefreshRun
{
    private static readonly Regex UrlPattern = new(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public DateTime StartedAtUtc { get; set; }

    public DateTime FinishedAtUtc { get; set; }

    public string Status { get; set; } = "Completed";

    public string Summary { get; set; } = string.Empty;

    public List<string> Warnings { get; set; } = new();

    internal void AddWarning(string message)
    {
        message = LimitText(message);
        if (Warnings.Count < 5 && !Warnings.Contains(message))
        {
            Warnings.Add(message);
        }
    }

    internal static string DescribeError(Exception exception)
        => LimitText(exception is HttpRequestException { StatusCode: { } statusCode }
            ? $"HTTP {(int)statusCode}: {exception.Message}"
            : exception.Message);

    internal static string LimitText(string message)
    {
        message = UrlPattern.Replace(message, "[upstream]");
        return message.Length <= 500 ? message : message[..499] + "…";
    }
}
