using System.Net;
using System.Text.RegularExpressions;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.LiveData;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>Refreshes the official guide pages used by item, reward, transfer, season, and purchase guidance.</summary>
public sealed partial class OfficialGuideSource
{
    private readonly OfficialPageFetcher _pages;

    private static readonly (int WikiNo, string Title, string Tags)[] Guides =
    {
        (259, "Beginner Guide Part 1", "beginner first character season starting choice interface progression"),
        (165, "Web Storage and Coupons", "coupon redeem web storage mail reward expiration"),
        (61, "Mail and Black Spirit's Safe", "mail black spirit safe challenge reward"),
        (13, "Inventory", "inventory binding item transfer slot weight"),
        (157, "Family Inventory", "family inventory eligible item transfer"),
        (43, "Storage", "town storage transport magnus inventory"),
        (150, "Maids and Butlers", "maid butler character transfer find my item ctrl f"),
        (21, "World Map", "world map family character move inventory transfer"),
        (47, "Central Market", "market warehouse tax sell item"),
        (237, "Season Server and Characters", "season character pass rewards"),
        (238, "Season Gear Guide", "season naru tuvala enhancement"),
        (241, "Season Graduation", "season graduation checklist reward"),
        (363, "Character Tag and Item Copy", "character tag item copy reroll class gear transfer restrictions"),
        (120, "Campsite", "campsite tent free premium pearl"),
        (254, "Checking Purchase History and Canceling Purchases", "pearl purchase cancellation refund"),
        (48, "Enhancement", "enhancement failstack cron memory advice valks"),
        (184, "Useful Tips - Quality of Life", "quality of life beginner interface"),
        (365, "Best Useful Tips Summary", "beginner progression useful tips"),
    };

    public OfficialGuideSource(HttpClient http, Func<string, CancellationToken, Task<string>>? renderedFallback = null) =>
        _pages = new OfficialPageFetcher(http, renderedFallback);

    public async Task<IReadOnlyList<KnowledgeRecord>> FetchAsync(Region region, CancellationToken ct = default)
    {
        var records = new List<KnowledgeRecord>();
        using var limiter = new SemaphoreSlim(4);
        var tasks = Guides.Select(async guide =>
        {
            await limiter.WaitAsync(ct);
            try
            {
                var url = BuildUrl(region, guide.WikiNo);
                var html = await _pages.GetAsync(url, ct, attempts: 2);
                var article = ExtractArticle(html, guide.Title);
                if (article.Length < 120) return;
                lock (records)
                {
                    records.Add(new KnowledgeRecord
                    {
                        SourceId = "official-guides",
                        ExternalId = guide.WikiNo.ToString(),
                        Kind = KnowledgeKinds.Guide,
                        Title = guide.Title,
                        Summary = KnowledgeText.Truncate(article, 650),
                        Content = article,
                        Url = url,
                        Region = RegionCode(region),
                        Tags = $"official guide {guide.Tags}",
                        Confidence = 1.0,
                        EffectiveAt = TryLastEdited(article),
                        RetrievedAt = DateTimeOffset.UtcNow,
                        ContentHash = KnowledgeText.Hash(guide.Title, article, url)
                    });
                }
            }
            catch when (!ct.IsCancellationRequested)
            {
                // Individual guide failures are tolerated; the curated offline record remains available.
            }
            finally { limiter.Release(); }
        });
        await Task.WhenAll(tasks);
        return records;
    }

    private static string BuildUrl(Region region, int wikiNo) => region switch
    {
        Region.SA => $"https://www.sa.playblackdesert.com/en-US/Wiki?wikiNo={wikiNo}",
        Region.Asia or Region.MENA => $"https://blackdesert.pearlabyss.com/Asia/en-US/Wiki?wikiNo={wikiNo}",
        _ => $"https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo={wikiNo}"
    };

    private static string RegionCode(Region region) => region.ToString().ToLowerInvariant();

    private static string ExtractArticle(string html, string title)
    {
        var decoded = WebUtility.HtmlDecode(html);
        var match = ArticleRegex().Match(decoded);
        var cleaned = KnowledgeText.CleanHtml(match.Success ? match.Groups[1].Value : decoded);
        var titleIndex = cleaned.IndexOf(title, StringComparison.OrdinalIgnoreCase);
        if (titleIndex > 0) cleaned = cleaned[titleIndex..];
        return KnowledgeText.Truncate(cleaned, 45_000);
    }

    private static DateTimeOffset? TryLastEdited(string text)
    {
        var match = LastEditedRegex().Match(text);
        return match.Success && DateTimeOffset.TryParse(match.Groups[1].Value, out var date) ? date : null;
    }

    [GeneratedRegex("<(?:article|div)[^>]+(?:class|id)=[\"'][^\"']*(?:wiki|contents|content|board)[^\"']*[\"'][^>]*>([\\s\\S]*?)</(?:article|div)>", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleRegex();

    [GeneratedRegex("Last Edited on\\s*:?\\s*([^\\n|<]{6,60})", RegexOptions.IgnoreCase)]
    private static partial Regex LastEditedRegex();
}
