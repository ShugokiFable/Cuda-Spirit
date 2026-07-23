using System.Net;
using System.Text.RegularExpressions;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.LiveData;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>
/// Indexes the official class catalog. A rendered-browser fallback can be supplied by the WPF host
/// when the official site serves an anti-bot challenge to HttpClient.
/// </summary>
public sealed partial class OfficialClassSource
{
    private readonly OfficialPageFetcher _pages;

    public OfficialClassSource(HttpClient http, Func<string, CancellationToken, Task<string>>? renderedFallback = null)
    {
        _pages = new OfficialPageFetcher(http, renderedFallback);
    }

    public async Task<IReadOnlyList<KnowledgeRecord>> FetchAsync(Region region, CancellationToken ct = default)
    {
        var url = BuildUrl(region);
        var html = await _pages.GetAsync(url, ct, attempts: 2);
        var text = KnowledgeText.CleanHtml(html, 80_000);
        var names = ParseClassNames(html, text);
        var now = DateTimeOffset.UtcNow;
        var records = new List<KnowledgeRecord>
        {
            new()
            {
                SourceId = "official-classes",
                ExternalId = "catalog",
                Kind = KnowledgeKinds.Guide,
                Title = "Official class catalog",
                Summary = names.Count == 0 ? "Official class page and class-selection information." : $"Current official class names: {string.Join(", ", names)}.",
                Content = text,
                Url = url,
                Region = region.ToString().ToLowerInvariant(),
                Tags = "official classes class selection succession awakening trial character",
                Confidence = 1.0,
                RetrievedAt = now,
                ContentHash = KnowledgeText.Hash(text, url)
            }
        };

        records.AddRange(names.Select(name => new KnowledgeRecord
        {
            SourceId = "official-classes",
            ExternalId = "class-" + KnowledgeText.Slug(name),
            Kind = KnowledgeKinds.Guide,
            Title = name,
            Summary = $"{name} appears in the current official Black Desert class catalog. Use live class notes and a trial character before permanent investment.",
            Content = $"Official class catalog entry: {name}. Current balance, skill behavior, and optimal builds should be checked against live patch notes.",
            Url = url,
            Region = region.ToString().ToLowerInvariant(),
            Tags = $"official class {name} trial succession awakening",
            Confidence = 1.0,
            RetrievedAt = now,
            ContentHash = KnowledgeText.Hash(name, url)
        }));
        return records;
    }

    private static IReadOnlyList<string> ParseClassNames(string html, string text)
    {
        var known = new[] { "Warrior", "Ranger", "Sorceress", "Berserker", "Tamer", "Musa", "Maehwa", "Valkyrie", "Kunoichi", "Ninja", "Wizard", "Witch", "Dark Knight", "Striker", "Mystic", "Lahn", "Archer", "Shai", "Guardian", "Hashashin", "Nova", "Sage", "Corsair", "Drakania", "Woosa", "Maegu", "Scholar", "Dosa", "Deadeye", "Wukong", "Seraph" };
        var haystack = WebUtility.HtmlDecode(html) + " " + text;
        return known.Where(name => Regex.IsMatch(haystack, $"(?<![A-Za-z]){Regex.Escape(name)}(?![A-Za-z])", RegexOptions.IgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildUrl(Region region) => region switch
    {
        Region.SA => "https://www.sa.playblackdesert.com/en-US/GameInfo/Class",
        Region.Asia or Region.MENA => "https://blackdesert.pearlabyss.com/Asia/en-US/GameInfo/Class",
        _ => "https://www.naeu.playblackdesert.com/en-US/GameInfo/Class"
    };
}
