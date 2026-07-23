using System.Net;
using System.Text.RegularExpressions;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.LiveData;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>Read-only ingestion of the official regional Black Desert update/patch-note listing.</summary>
public sealed partial class OfficialPatchNotesSource
{
    private readonly OfficialPageFetcher _pages;

    public OfficialPatchNotesSource(HttpClient http, Func<string, CancellationToken, Task<string>>? renderedFallback = null) =>
        _pages = new OfficialPageFetcher(http, renderedFallback);

    public async Task<IReadOnlyList<KnowledgeRecord>> FetchAsync(Region region, int detailLimit = 8, CancellationToken ct = default)
    {
        var listingUrl = ListingUrl(region);
        var html = await _pages.GetAsync(listingUrl, ct);
        var links = ParseLinks(html, listingUrl)
            .Where(x => IsPatchLike(x.Title))
            .DistinctBy(x => x.Id)
            .Take(24)
            .ToList();

        var records = links.Select(x => new KnowledgeRecord
        {
            SourceId = "official-patch-notes",
            ExternalId = x.Id,
            Kind = KnowledgeKinds.PatchNote,
            Title = x.Title,
            Summary = x.Title,
            Content = x.Title,
            Url = x.Url,
            Region = RegionCode(region),
            Tags = "official patch update live",
            EffectiveAt = x.Date,
            RetrievedAt = DateTimeOffset.UtcNow,
            Confidence = 1.0,
            ContentHash = KnowledgeText.Hash(x.Title, x.Url)
        }).ToDictionary(x => x.ExternalId);

        // Pull a bounded number of recent details. Failure of one article does not poison the source.
        using var limiter = new SemaphoreSlim(3);
        var detailTasks = links.Take(Math.Clamp(detailLimit, 0, 16)).Select(async link =>
        {
            await limiter.WaitAsync(ct);
            try
            {
                var detailHtml = await _pages.GetAsync(link.Url, ct, attempts: 2);
                var cleaned = ExtractArticle(detailHtml);
                if (cleaned.Length < 80) return;
                var existing = records[link.Id];
                existing.Content = cleaned;
                existing.Summary = KnowledgeText.Truncate(cleaned, 600);
                existing.ContentHash = KnowledgeText.Hash(existing.Title, existing.Content);
            }
            catch when (!ct.IsCancellationRequested)
            {
                // Keep the listing record. Its title/date still helps freshness reasoning.
            }
            finally
            {
                limiter.Release();
            }
        });
        await Task.WhenAll(detailTasks);
        return records.Values.OrderByDescending(x => x.EffectiveAt ?? x.RetrievedAt).ToList();
    }

    private static bool IsPatchLike(string title)
    {
        var t = title.ToLowerInvariant();
        return t.Contains("patch") || t.Contains("update") || t.Contains("maintenance") ||
               t.Contains("hotfix") || t.Contains("last update") || t.Contains("latest update");
    }

    private static string ListingUrl(Region region) => region switch
    {
        Region.SA => "https://www.sa.playblackdesert.com/en-US/News/Notice?boardType=2",
        Region.Asia or Region.MENA => "https://blackdesert.pearlabyss.com/Asia/en-US/News/Notice?boardType=2",
        Region.Console => "https://blackdesert.pearlabyss.com/Console/en-US/News/Notice?_categoryNo=2",
        _ => "https://www.naeu.playblackdesert.com/en-US/News/Notice?boardType=2"
    };

    private static string RegionCode(Region region) => region.ToString().ToLowerInvariant();

    private static IReadOnlyList<PatchLink> ParseLinks(string html, string baseUrl)
    {
        var result = new List<PatchLink>();
        foreach (Match match in DetailLinkRegex().Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["url"].Value);
            var title = KnowledgeText.CleanHtml(match.Groups["title"].Value, 500);
            if (string.IsNullOrWhiteSpace(title)) continue;
            var idMatch = BoardNumberRegex().Match(href);
            var id = idMatch.Success ? idMatch.Groups[1].Value : KnowledgeText.Hash(href)[..16];
            var absolute = Uri.TryCreate(href, UriKind.Absolute, out var abs)
                ? abs.ToString()
                : new Uri(new Uri(baseUrl), href).ToString();

            var aroundStart = Math.Max(0, match.Index - 300);
            var aroundLength = Math.Min(html.Length - aroundStart, match.Length + 600);
            var around = html.Substring(aroundStart, aroundLength);
            result.Add(new PatchLink(id, title, absolute, TryParseDate(around)));
        }
        return result;
    }

    private static DateTimeOffset? TryParseDate(string text)
    {
        foreach (Match m in DateRegex().Matches(text))
        {
            if (DateTimeOffset.TryParse(m.Value, out var value)) return value;
        }
        return null;
    }

    private static string ExtractArticle(string html)
    {
        var body = ArticleRegex().Match(html);
        return KnowledgeText.CleanHtml(body.Success ? body.Groups[1].Value : html);
    }

    private sealed record PatchLink(string Id, string Title, string Url, DateTimeOffset? Date);

    [GeneratedRegex("<a[^>]+href=[\"'](?<url>[^\"']*News/Notice/Detail[^\"']*)[\"'][^>]*>(?<title>[\\s\\S]*?)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex DetailLinkRegex();

    [GeneratedRegex("(?:_boardNo|boardNo)=([0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BoardNumberRegex();

    [GeneratedRegex("(?:20[0-9]{2}[-./][01]?[0-9][-./][0-3]?[0-9])|(?:[01]?[0-9][-./][0-3]?[0-9][-./]20[0-9]{2})")]
    private static partial Regex DateRegex();

    [GeneratedRegex("<(?:article|div)[^>]+(?:class|id)=[\"'][^\"']*(?:article|contents|content|board)[^\"']*[\"'][^>]*>([\\s\\S]*?)</(?:article|div)>", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleRegex();
}
