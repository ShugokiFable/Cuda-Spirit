namespace CudaSpirit.Core.Models;

public enum AdventurerStage
{
    BrandNew,
    SeasonEarly,
    SeasonLate,
    Graduated,
    Midgame,
    Endgame,
    LifeSkillFocused,
    Returning
}

public enum PlayFocus
{
    Guided,
    PvE,
    LifeSkills,
    PvP,
    Collecting,
    Casual
}

public enum SpendingStyle
{
    FreeToPlay,
    LowSpender,
    ValueBuyer,
    ConvenienceFirst,
    Whale
}

public enum GuidanceVerdict
{
    Stop,
    Keep,
    Store,
    Transfer,
    UseNow,
    UseLater,
    Sell,
    Conditional,
    Unknown
}

public enum ItemBinding
{
    Unknown,
    Unbound,
    FamilyBound,
    CharacterBound,
    AccountLimited,
    Expiring
}

public sealed class ItemGuidanceRequest
{
    public string ItemName { get; set; } = "";
    public string TooltipText { get; set; } = "";
    public string CurrentCharacter { get; set; } = "";
    public ItemBinding Binding { get; set; }
    public bool IsSeasonCharacter { get; set; }
}

public sealed class ItemGuidanceResult
{
    public GuidanceVerdict Verdict { get; set; }
    public string Headline { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string BestLocation { get; set; } = "";
    public string TransferAdvice { get; set; } = "";
    public string BindingWarning { get; set; } = "";
    public int ConfidencePercent { get; set; }
    public IReadOnlyList<string> BeforeYouAct { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> MatchedRules { get; set; } = Array.Empty<string>();
}

public sealed class TransferRequest
{
    public string ItemName { get; set; } = "";
    public ItemBinding Binding { get; set; }
    public bool IsMarketable { get; set; }
    public bool IsTradeGood { get; set; }
    public bool IsGuildItem { get; set; }
    public bool IsTreasureItem { get; set; }
    public bool HasStorageMaid { get; set; }
    public bool HasTransactionMaid { get; set; }
    public bool MagnusStorageUnlocked { get; set; }
    public bool FamilyInventoryEligible { get; set; }
}

public sealed class TransferResult
{
    public bool CanTransfer { get; set; }
    public string Summary { get; set; } = "";
    public IReadOnlyList<string> RecommendedMethods { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Blockers { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Steps { get; set; } = Array.Empty<string>();
}

public sealed class PearlOfferInput
{
    public string Name { get; set; } = "";
    public string ContentsText { get; set; } = "";
    public int PricePearls { get; set; }
    public int OriginalPricePearls { get; set; }
    public int HoursUntilOfferEnds { get; set; } = -1;
    public bool RandomContents { get; set; }
    public bool CharacterBound { get; set; }
    public bool PermanentFamilyWide { get; set; }
    public bool HasFreeAlternative { get; set; }
    public bool AlreadyOwnEquivalent { get; set; }
    public bool MostlyConsumablesOrPadding { get; set; }
    public bool WouldBuyContentsIndividually { get; set; }
}

public sealed class PearlOfferResult
{
    public int Score { get; set; }
    public string Verdict { get; set; } = "";
    public string Summary { get; set; } = "";
    public IReadOnlyList<string> Positives { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class CompanionTask
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Category { get; set; } = "general";
    public string Cadence { get; set; } = "once";
    public int Priority { get; set; } = 50;
    public DateTimeOffset? DueAt { get; set; }
    public string Status { get; set; } = "open";
    public bool Pinned { get; set; }
    public string SourceUrl { get; set; } = "";
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ItemDecisionHistory
{
    public long Id { get; set; }
    public string ItemName { get; set; } = "";
    public string Verdict { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Binding { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PearlEvaluationHistory
{
    public long Id { get; set; }
    public string OfferName { get; set; } = "";
    public int PricePearls { get; set; }
    public int OriginalPricePearls { get; set; }
    public int Score { get; set; }
    public string Verdict { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;
}
