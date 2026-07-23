using System.Text;
using System.Text.RegularExpressions;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>Retrieves a small, source-attributed slice of the local database for each AI request.</summary>
public sealed partial class KnowledgeRetriever
{
    private readonly AppDatabase _db;

    public KnowledgeRetriever(AppDatabase db) => _db = db;

    public string BuildAdvisorContext(string userPrompt, Region region, int maxRecords = 8, int maxCharacters = 16_000)
    {
        var query = BuildFtsQuery(userPrompt);
        var hits = _db.SearchKnowledge(query, region.ToString().ToLowerInvariant(), maxRecords);

        // Always add one current patch-note record when the semantic query did not retrieve one.
        var records = hits.Select(x => x.Record).ToList();
        if (records.All(x => x.Kind != KnowledgeKinds.PatchNote))
        {
            var latestPatch = _db.GetLatestKnowledge(1, KnowledgeKinds.PatchNote, region.ToString().ToLowerInvariant()).FirstOrDefault();
            if (latestPatch is not null) records.Add(latestPatch);
        }

        var states = _db.GetSourceStates();
        var sb = new StringBuilder();
        sb.AppendLine("LOCAL_KNOWLEDGE_STATUS:");
        foreach (var state in states.OrderByDescending(x => x.LastSuccessAt).Take(8))
        {
            var freshness = state.LastSuccessAt is { } success
                ? $"last success {success:O}"
                : "never synced";
            sb.Append("- ").Append(state.DisplayName).Append(": ").Append(state.Status)
              .Append(", ").Append(freshness).Append(", records ").Append(state.LastRecordCount).AppendLine();
        }

        sb.AppendLine("RETRIEVED_BDO_KNOWLEDGE:");
        foreach (var record in records.DistinctBy(x => (x.SourceId, x.ExternalId)))
        {
            var effective = record.EffectiveAt?.ToString("O") ?? "unknown";
            sb.Append("[source=").Append(record.SourceId)
              .Append(" kind=").Append(record.Kind)
              .Append(" region=").Append(record.Region)
              .Append(" effective=").Append(effective)
              .Append(" retrieved=").Append(record.RetrievedAt.ToString("O"))
              .Append(" confidence=").Append(record.Confidence.ToString("0.00"))
              .AppendLine("]");
            sb.AppendLine(record.Title);
            var body = string.IsNullOrWhiteSpace(record.Summary) ? record.Content : record.Summary + "\n" + record.Content;
            var remaining = maxCharacters - sb.Length;
            if (remaining <= 200) break;
            sb.AppendLine(KnowledgeText.Truncate(body, Math.Min(2_500, remaining - 100)));
            if (!string.IsNullOrWhiteSpace(record.Url)) sb.AppendLine("URL: " + record.Url);
            sb.AppendLine();
        }

        if (records.Count == 0)
            sb.AppendLine("No matching records are stored yet. State this limitation and recommend syncing/importing data rather than inventing current values.");

        return KnowledgeText.Truncate(sb.ToString(), maxCharacters);
    }

    public IReadOnlyList<KnowledgeSearchHit> Search(string query, Region region, int limit = 25) =>
        _db.SearchKnowledge(BuildFtsQuery(query), region.ToString().ToLowerInvariant(), limit);

    private static string BuildFtsQuery(string prompt)
    {
        var tokens = TokenRegex().Matches(prompt ?? "")
            .Select(x => x.Value.ToLowerInvariant())
            .Where(x => x.Length >= 3 && !StopWords.Contains(x))
            .Distinct()
            .Take(14)
            .Select(x => $"\"{x.Replace("\"", "\"\"")}\"")
            .ToList();
        return tokens.Count == 0 ? "" : string.Join(" OR ", tokens);
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "that", "this", "what", "how", "from", "into", "your", "best",
        "black", "desert", "online", "bdo", "please", "should", "would", "could", "about", "latest"
    };

    [GeneratedRegex("[\\p{L}\\p{N}_-]+")]
    private static partial Regex TokenRegex();
}
