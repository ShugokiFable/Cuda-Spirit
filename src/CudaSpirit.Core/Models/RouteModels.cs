namespace CudaSpirit.Core.Models;

public enum RouteObjective
{
    Balanced,
    Fastest,
    HighestSilver,
    LowestRisk
}

/// <summary>A graph node used by the advisory route optimizer.</summary>
public sealed class RouteNode
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Territory { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public int RecommendedAp { get; set; }
    public int RecommendedDp { get; set; }
    public long ExpectedSilverPerHour { get; set; }
    public double Risk { get; set; }
    public string Tags { get; set; } = "";
    public string SourceId { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RouteEdge
{
    public long Id { get; set; }
    public string FromKey { get; set; } = "";
    public string ToKey { get; set; } = "";
    public double TravelMinutes { get; set; }
    public double Risk { get; set; }
    public bool Bidirectional { get; set; } = true;
    public string Transport { get; set; } = "ground";
    public string SourceId { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RoutePlanRequest
{
    public string StartKey { get; set; } = "";
    public string DestinationKey { get; set; } = "";
    public RouteObjective Objective { get; set; } = RouteObjective.Balanced;
    public int PlayerAp { get; set; }
    public int PlayerDp { get; set; }
    public double RiskTolerance { get; set; } = 0.5;
    public long SilverPerMinuteValue { get; set; } = 10_000_000;
}

public sealed class RouteStep
{
    public int Order { get; set; }
    public RouteNode Node { get; set; } = new();
    public double TravelMinutesFromPrevious { get; set; }
    public string Instruction { get; set; } = "";
}

public sealed class RoutePlan
{
    public bool Found { get; set; }
    public string Message { get; set; } = "";
    public RouteObjective Objective { get; set; }
    public IReadOnlyList<RouteStep> Steps { get; set; } = Array.Empty<RouteStep>();
    public double TotalTravelMinutes { get; set; }
    public double TotalRisk { get; set; }
    public long DestinationSilverPerHour { get; set; }
}

public sealed class FarmRecommendation
{
    public RouteNode Zone { get; init; } = new();
    public double Score { get; init; }
    public string Fit { get; init; } = "";
    public string Reason { get; init; } = "";
}
