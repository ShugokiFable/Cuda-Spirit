namespace CudaSpirit.Core.Models;

/// <summary>Normalized knowledge kinds stored in the local advisor database.</summary>
public static class KnowledgeKinds
{
    public const string PatchNote = "patch-note";
    public const string Market = "market";
    public const string Item = "item";
    public const string Recipe = "recipe";
    public const string Node = "node";
    public const string GrindZone = "grind-zone";
    public const string Skill = "skill";
    public const string Boss = "boss";
    public const string Reset = "reset";
    public const string News = "news";
    public const string Maintenance = "maintenance";
    public const string Coupon = "coupon";
    public const string Guide = "guide";
    public const string Event = "event";
    public const string PearlShop = "pearl-shop";
    public const string ItemSafety = "item-safety";
    public const string Other = "other";
}

/// <summary>
/// One normalized record from an external or local source. Records are deduplicated by
/// (SourceId, ExternalId), hashed, timestamped, and indexed for AI retrieval.
/// </summary>
public sealed class KnowledgeRecord
{
    public long Id { get; set; }
    public string SourceId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Kind { get; set; } = KnowledgeKinds.Other;
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Content { get; set; } = "";
    public string Url { get; set; } = "";
    public string Region { get; set; } = "global";
    public string Tags { get; set; } = "";
    public string MetadataJson { get; set; } = "{}";
    public string ContentHash { get; set; } = "";
    public double Confidence { get; set; } = 0.8;
    public DateTimeOffset? EffectiveAt { get; set; }
    public DateTimeOffset RetrievedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsExpired => ExpiresAt is { } expires && expires <= DateTimeOffset.UtcNow;
}

public sealed class KnowledgeSearchHit
{
    public KnowledgeRecord Record { get; init; } = new();
    public double Rank { get; init; }
}

/// <summary>Last known health/freshness of an ingest source.</summary>
public sealed class DataSourceState
{
    public string SourceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "never";
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public int LastRecordCount { get; set; }
    public string LastError { get; set; } = "";
    public string Cursor { get; set; } = "";
    public string MetadataJson { get; set; } = "{}";
}

public sealed class DatabaseStats
{
    public int KnowledgeRecords { get; set; }
    public int MarketSnapshots { get; set; }
    public int MarketHistoryPoints { get; set; }
    public int RouteNodes { get; set; }
    public int RouteEdges { get; set; }
    public int ImportedFiles { get; set; }
    public int Sources { get; set; }
    public DateTimeOffset? FreshestKnowledgeAt { get; set; }
}

public sealed class SyncSourceResult
{
    public string SourceId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public bool Success { get; init; }
    public int Records { get; init; }
    public string Message { get; init; } = "";
    public TimeSpan Duration { get; init; }
}

public sealed class SyncReport
{
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public IReadOnlyList<SyncSourceResult> Sources { get; init; } = Array.Empty<SyncSourceResult>();
    public int TotalRecords => Sources.Sum(x => x.Records);
    public bool Success => Sources.Count > 0 && Sources.All(x => x.Success);
}
