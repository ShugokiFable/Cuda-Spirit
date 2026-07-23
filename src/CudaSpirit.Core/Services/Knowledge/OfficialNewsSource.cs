using System.Net;
using System.Text.RegularExpressions;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.LiveData;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>Reads current official event and Pearl Shop listings, including bounded article details.</summary>
public sealed partial class OfficialNewsSource
{
    private readonly OfficialPageFetcher _pages;

    public OfficialNewsSource(HttpClient http, Func<string, CancellationToken, Task<string>>? renderedFallback = null) =>
        _pages = new OfficialPageFetcher(http, renderedFallback);

    public Task<IReadOnlyList<KnowledgeRecord>> FetchEventsAsync(Region region, int details, CancellationToken ct = default) =>
        FetchCategoryAsync(region, boardType: 3, sourceId: "official-events", kind: KnowledgeKinds.Event,
            titleFilter: title => !title.Contains("Pearl Shop", StringComparison.OrdinalIgnoreCase) && !title.Contains("Patch Notes", StringComparison.OrdinalIgnoreCase),
            details, ct);

    public Task<IReadOnlyList<KnowledgeRecord>> FetchPearlShopAsync(Region region, int details, CancellationToken ct = default) =>
        FetchCategoryAsync(region, boardType: 5, sourceId: "official-pearl-shop", kind: KnowledgeKinds.PearlShop,
            titleFilter: title => title.Contains("Pearl", StringComparison.OrdinalIgnoreCase) || title.Contains("Outfit", StringComparison.OrdinalIgnoreCase),
            details, ct);

    private async Task<IReadOnlyList<KnowledgeRecord>> FetchCategoryAsync(
        Region region, int boardType, string sourceId, string kind, Func<string, bool> titleFilter, int details, CancellationToken ct)
    {
        var listing = ListingUrl(region, boardType);
        var html = await _pages.GetAsync(listing, ct);
        var links = ParseLinks(html, listing)
            .Where(x => titleFilter(x.Title))
            .DistinctBy(x => x.Id)
            .Take(40)
            .ToList();

        var records = links.ToDictionary(x => x.Id, x => new KnowledgeRecord
        {
            SourceId = sourceId,
            ExternalId = x.Id,
            Kind = kind,
            Title = x.Title,
            Summary = x.Title,
            Content = x.Title,
            Url = x.Url,
            Region = RegionCode(region),
            Tags = kind == KnowledgeKinds.PearlShop ? "official pearl shop offer sale bundle discount" : "official active event reward deadline",
            EffectiveAt = x.Date,
            RetrievedAt = DateTimeOffset.UtcNow,
            Confidence = 1.0,
            ContentHash = KnowledgeText.Hash(x.Title, x.Url)
        });

        using var limiter = new SemaphoreSlim(3);
        var tasks = links.Take(Math.Clamp(details, 0, 20)).Select(async link =>
        {
            await limiter.WaitAsync(ct);
            try
            {
                var detailHtml = await _pages.GetAsync(link.Url, ct, attempts: 2);
                var content = ExtractArticle(detailHtml);
                if (content.Length < 80) return;
                var item = records[link.Id];
                item.Content = content;
                item.Summary = KnowledgeText.Truncate(content, 700);
                item.ExpiresAt = TryFindEndDate(content);
                item.ContentHash = KnowledgeText.Hash(item.Title, item.Content);
            }
            catch when (!ct.IsCancellationRequested) { }
            finally { limiter.Release(); }
        });
        await Task.WhenAll(tasks);
        return records.Values.OrderByDescending(x => x.EffectiveAt ?? x.RetrievedAt).ToList();
    }

    private static string ListingUrl(Region region, int boardType) => region switch
    {
        Region.SA => $"https://www.sa.playblackdesert.com/en-US/News/Notice?boardType={boardType}",
        Region.Asia or Region.MENA => $"https://blackdesert.pearlabyss.com/Asia/en-US/News/Notice?boardType={boardType}",
        Region.Console => $"https://blackdesert.pearlabyss.com/Console/en-US/News/Notice?_categoryNo={boardType}",
        _ => $"https://www.naeu.playblackdesert.com/en-US/News/Notice?boardType={boardType}"
    };

    private static string RegionCode(Region region) => region.ToString().ToLowerInvariant();

    private static IReadOnlyList<NewsLink> ParseLinks(string html, string baseUrl)
    {
        var result = new List<NewsLink>();
        foreach (Match match in DetailLinkRegex().Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["url"].Value);
            var title = KnowledgeText.CleanHtml(match.Groups["title"].Value, 500);
            if (string.IsNullOrWhiteSpace(title)) continue;
            var idMatch = BoardNumberRegex().Match(href);
            var id = idMatch.Success ? idMatch.Groups[1].Value : KnowledgeText.Hash(href)[..16];
            var absolute = Uri.TryCreate(href, UriKind.Absolute, out var abs) ? abs.ToString() : new Uri(new Uri(baseUrl), href).ToString();
            var aroundStart = Math.Max(0, match.Index - 320);
            var aroundLength = Math.Min(html.Length - aroundStart, match.Length + 720);
            result.Add(new NewsLink(id, title, absolute, TryParseDate(html.Substring(aroundStart, aroundLength))));
        }
        return result;
    }

    private static DateTimeOffset? TryParseDate(string text)
    {
        foreach (Match m in DateRegex().Matches(text))
            if (DateTimeOffset.TryParse(m.Value, out var value)) return value;
        return null;
    }

    private static DateTimeOffset? TryFindEndDate(string text)
    {
        var matches = DateRegex().Matches(text).Select(x => x.Value)
            .Select(x => DateTimeOffset.TryParse(x, out var d) ? d : (DateTimeOffset?)null)
            .Where(x => x is not null && x > DateTimeOffset.UtcNow.AddDays(-2))
            .Select(x => x!.Value).OrderByDescending(x => x).ToList();
        return matches.Count > 0 ? matches[0] : null;
    }

    private static string ExtractArticle(string html)
    {
        var body = ArticleRegex().Match(html);
        return KnowledgeText.Truncate(KnowledgeText.CleanHtml(body.Success ? body.Groups[1].Value : html), 60_000);
    }

    private sealed record NewsLink(string Id, string Title, string Url, DateTimeOffset? Date);

    [GeneratedRegex("<a[^>]+href=[\"'](?<url>[^\"']*News/(?:Notice/)?Detail[^\"']*)[\"'][^>]*>(?<title>[\\s\\S]*?)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex DetailLinkRegex();
    [GeneratedRegex("(?:groupContentNo|_boardNo|boardNo)=([0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BoardNumberRegex();
    [GeneratedRegex("(?:20[0-9]{2}[-./][01]?[0-9][-./][0-3]?[0-9])|(?:[01]?[0-9][-./][0-3]?[0-9][-./]20[0-9]{2})")]
    private static partial Regex DateRegex();
    [GeneratedRegex("<(?:article|div)[^>]+(?:class|id)=[\"'][^\"']*(?:article|contents|content|board)[^\"']*[\"'][^>]*>([\\s\\S]*?)</(?:article|div)>", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleRegex();
}
