namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// Stores the last Index page query string so that other pages
/// (e.g. Tutorial "Try this") can navigate back with full state.
/// </summary>
public sealed class NavigationStateService
{
    /// <summary>
    /// The last-known Index page URL including query string
    /// (e.g. "/?algo=Bubble+sort&amp;size=1024&amp;pattern=…&amp;mode=BarChart&amp;cards=…").
    /// </summary>
    public string? LastIndexUrl { get; set; }

    /// <summary>
    /// Builds an Index URL that preserves the saved state but overrides the <c>algo</c> parameter.
    /// Falls back to <c>/?algo={algoName}</c> when no saved state exists.
    /// </summary>
    public string BuildIndexUrlWithAlgorithm(string algoName)
    {
        if (string.IsNullOrEmpty(LastIndexUrl))
            return $"/?algo={Uri.EscapeDataString(algoName)}";

        var escaped = Uri.EscapeDataString(algoName);

        // The URL format is: /?algo=...&size=...&pattern=...&mode=...[&cards=...]
        // Replace the algo parameter value.
        var url = LastIndexUrl;
        var algoStart = url.IndexOf("algo=", StringComparison.Ordinal);
        if (algoStart >= 0)
        {
            algoStart += "algo=".Length;
            var algoEnd = url.IndexOf('&', algoStart);
            url = algoEnd >= 0
                ? string.Concat(url.AsSpan(0, algoStart), escaped, url.AsSpan(algoEnd))
                : string.Concat(url.AsSpan(0, algoStart), escaped);
        }

        return url;
    }
}
