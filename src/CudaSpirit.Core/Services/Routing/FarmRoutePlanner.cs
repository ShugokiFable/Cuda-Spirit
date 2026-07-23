using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;

namespace CudaSpirit.Core.Services.Routing;

/// <summary>
/// Advisory graph optimizer for travel and farm selection. It never sends input to the game,
/// attaches to the process, or controls the character.
/// </summary>
public sealed class FarmRoutePlanner
{
    private readonly AppDatabase _db;

    public FarmRoutePlanner(AppDatabase db) => _db = db;

    public IReadOnlyList<FarmRecommendation> RecommendFarms(
        int playerAp,
        int playerDp,
        string? startKey = null,
        RouteObjective objective = RouteObjective.Balanced,
        double riskTolerance = 0.5,
        int limit = 10)
    {
        var nodes = _db.GetRouteNodes();
        var edges = _db.GetRouteEdges();
        var travel = string.IsNullOrWhiteSpace(startKey)
            ? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            : DistancesFrom(startKey, nodes, edges, objective, riskTolerance);

        return nodes
            .Where(n => n.ExpectedSilverPerHour > 0)
            .Select(n =>
            {
                var apDeficit = Math.Max(0, n.RecommendedAp - playerAp);
                var dpDeficit = Math.Max(0, n.RecommendedDp - playerDp);
                var unsafePenalty = apDeficit * 0.03 + dpDeficit * 0.015;
                var riskPenalty = Math.Max(0, n.Risk - riskTolerance) * 2.5;
                var travelMinutes = travel.TryGetValue(n.Key, out var t) ? t : EstimateDirectTravel(startKey, n, nodes);
                var travelPenalty = Math.Min(1.5, travelMinutes / 90.0);
                var profitScore = Math.Log10(Math.Max(1, n.ExpectedSilverPerHour));
                var objectiveBoost = objective switch
                {
                    RouteObjective.HighestSilver => profitScore * 1.35,
                    RouteObjective.LowestRisk => profitScore * 0.85 - n.Risk * 2,
                    RouteObjective.Fastest => profitScore * 0.75 - travelPenalty * 1.5,
                    _ => profitScore
                };
                var score = objectiveBoost - unsafePenalty - riskPenalty - travelPenalty;
                var fit = apDeficit == 0 && dpDeficit == 0 ? "Ready" : $"Needs +{apDeficit} AP / +{dpDeficit} DP";
                var reason = $"{n.ExpectedSilverPerHour:N0}/hr; travel ~{travelMinutes:0.#}m; risk {n.Risk:0.00}.";
                return new FarmRecommendation { Zone = n, Score = score, Fit = fit, Reason = reason };
            })
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();
    }

    public RoutePlan Plan(RoutePlanRequest request)
    {
        var nodes = _db.GetRouteNodes().ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var edges = _db.GetRouteEdges();
        if (!nodes.TryGetValue(request.StartKey, out var start))
            return NotFound(request, "Start node is not in the route database.");
        if (!nodes.TryGetValue(request.DestinationKey, out var destination))
            return NotFound(request, "Destination node is not in the route database.");
        if (start.Key.Equals(destination.Key, StringComparison.OrdinalIgnoreCase))
            return new RoutePlan
            {
                Found = true,
                Objective = request.Objective,
                Message = "Already at the selected destination.",
                Steps = new[] { new RouteStep { Order = 1, Node = start, Instruction = $"Farm at {start.Name}." } },
                DestinationSilverPerHour = destination.ExpectedSilverPerHour
            };

        var adjacency = BuildAdjacency(edges);
        var dist = nodes.Keys.ToDictionary(x => x, _ => double.PositiveInfinity, StringComparer.OrdinalIgnoreCase);
        var previous = new Dictionary<string, (string Parent, RouteEdge Edge)>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, double>();
        dist[start.Key] = 0;
        queue.Enqueue(start.Key, 0);

        while (queue.TryDequeue(out var current, out var currentCost))
        {
            if (currentCost > dist[current]) continue;
            if (current.Equals(destination.Key, StringComparison.OrdinalIgnoreCase)) break;
            if (!adjacency.TryGetValue(current, out var outgoing)) continue;

            foreach (var edge in outgoing)
            {
                var next = edge.FromKey.Equals(current, StringComparison.OrdinalIgnoreCase) ? edge.ToKey : edge.FromKey;
                if (!nodes.ContainsKey(next)) continue;
                var cost = EdgeCost(edge, nodes[next], request);
                var candidate = currentCost + cost;
                if (candidate >= dist[next]) continue;
                dist[next] = candidate;
                previous[next] = (current, edge);
                queue.Enqueue(next, candidate);
            }
        }

        if (!previous.ContainsKey(destination.Key))
        {
            // A coordinate-based direct advisory fallback is useful for partially imported datasets.
            var direct = EstimateDirectTravel(start.Key, destination, nodes.Values);
            if (direct <= 0) return NotFound(request, "No connected route exists. Import route edges or choose another pair.");
            return new RoutePlan
            {
                Found = true,
                Objective = request.Objective,
                Message = "No graph edge connected these nodes, so this is a coordinate-based direct estimate.",
                TotalTravelMinutes = direct,
                TotalRisk = destination.Risk,
                DestinationSilverPerHour = destination.ExpectedSilverPerHour,
                Steps = new[]
                {
                    new RouteStep { Order = 1, Node = start, Instruction = $"Start at {start.Name}." },
                    new RouteStep { Order = 2, Node = destination, TravelMinutesFromPrevious = direct, Instruction = $"Travel manually to {destination.Name}; estimated {direct:0.#} minutes." }
                }
            };
        }

        var pathNodes = new List<RouteNode> { destination };
        var pathEdges = new List<RouteEdge>();
        var cursor = destination.Key;
        while (!cursor.Equals(start.Key, StringComparison.OrdinalIgnoreCase))
        {
            var p = previous[cursor];
            pathEdges.Add(p.Edge);
            cursor = p.Parent;
            pathNodes.Add(nodes[cursor]);
        }
        pathNodes.Reverse();
        pathEdges.Reverse();

        var steps = new List<RouteStep>();
        double totalMinutes = 0;
        double totalRisk = 0;
        for (var i = 0; i < pathNodes.Count; i++)
        {
            var travelEdge = i == 0 ? null : pathEdges[i - 1];
            var minutes = travelEdge?.TravelMinutes ?? 0;
            totalMinutes += minutes;
            totalRisk += travelEdge?.Risk ?? 0;
            steps.Add(new RouteStep
            {
                Order = i + 1,
                Node = pathNodes[i],
                TravelMinutesFromPrevious = minutes,
                Instruction = i == 0
                    ? $"Start at {pathNodes[i].Name}."
                    : $"Travel via {travelEdge!.Transport} to {pathNodes[i].Name} (~{minutes:0.#}m)."
            });
        }
        steps[^1].Instruction += destination.ExpectedSilverPerHour > 0
            ? $" Estimated farm value: {destination.ExpectedSilverPerHour:N0} silver/hour."
            : "";

        return new RoutePlan
        {
            Found = true,
            Objective = request.Objective,
            Message = "Route optimized from the imported graph. You remain in control of all movement and actions.",
            Steps = steps,
            TotalTravelMinutes = totalMinutes,
            TotalRisk = totalRisk,
            DestinationSilverPerHour = destination.ExpectedSilverPerHour
        };
    }

    private static RoutePlan NotFound(RoutePlanRequest request, string message) => new()
    {
        Found = false,
        Objective = request.Objective,
        Message = message
    };

    private static Dictionary<string, List<RouteEdge>> BuildAdjacency(IReadOnlyList<RouteEdge> edges)
    {
        var adjacency = new Dictionary<string, List<RouteEdge>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
        {
            if (!adjacency.TryGetValue(edge.FromKey, out var from)) adjacency[edge.FromKey] = from = new List<RouteEdge>();
            from.Add(edge);
            if (edge.Bidirectional)
            {
                if (!adjacency.TryGetValue(edge.ToKey, out var to)) adjacency[edge.ToKey] = to = new List<RouteEdge>();
                to.Add(edge);
            }
        }
        return adjacency;
    }

    private static double EdgeCost(RouteEdge edge, RouteNode destination, RoutePlanRequest request)
    {
        var gearPenalty = Math.Max(0, destination.RecommendedAp - request.PlayerAp) * 0.5 +
                          Math.Max(0, destination.RecommendedDp - request.PlayerDp) * 0.25;
        var riskPenalty = Math.Max(0, edge.Risk - request.RiskTolerance) * 45 +
                          Math.Max(0, destination.Risk - request.RiskTolerance) * 30;
        var profitCredit = destination.ExpectedSilverPerHour <= 0 ? 0 :
            destination.ExpectedSilverPerHour / (double)Math.Max(1, request.SilverPerMinuteValue);

        var cost = request.Objective switch
        {
            RouteObjective.Fastest => edge.TravelMinutes + riskPenalty * 0.25,
            RouteObjective.HighestSilver => edge.TravelMinutes - profitCredit + gearPenalty + riskPenalty * 0.4,
            RouteObjective.LowestRisk => edge.TravelMinutes * 0.5 + riskPenalty * 2 + destination.Risk * 60 + gearPenalty,
            _ => edge.TravelMinutes + gearPenalty + riskPenalty - profitCredit * 0.35
        };

        // Dijkstra requires non-negative edge costs. Profit is a credit, never a negative travel loop.
        return Math.Max(0.25, cost);
    }

    private static Dictionary<string, double> DistancesFrom(
        string startKey,
        IReadOnlyList<RouteNode> nodes,
        IReadOnlyList<RouteEdge> edges,
        RouteObjective objective,
        double riskTolerance)
    {
        var nodeMap = nodes.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        if (!nodeMap.ContainsKey(startKey)) return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var adjacency = BuildAdjacency(edges);
        var dist = nodeMap.Keys.ToDictionary(x => x, _ => double.PositiveInfinity, StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, double>();
        dist[startKey] = 0;
        queue.Enqueue(startKey, 0);
        while (queue.TryDequeue(out var current, out var currentCost))
        {
            if (currentCost > dist[current]) continue;
            if (!adjacency.TryGetValue(current, out var outgoing)) continue;
            foreach (var edge in outgoing)
            {
                var next = edge.FromKey.Equals(current, StringComparison.OrdinalIgnoreCase) ? edge.ToKey : edge.FromKey;
                if (!nodeMap.ContainsKey(next)) continue;
                var cost = objective == RouteObjective.LowestRisk
                    ? edge.TravelMinutes + Math.Max(0, edge.Risk - riskTolerance) * 60
                    : edge.TravelMinutes;
                var candidate = currentCost + cost;
                if (candidate >= dist[next]) continue;
                dist[next] = candidate;
                queue.Enqueue(next, candidate);
            }
        }
        return dist.Where(x => !double.IsPositiveInfinity(x.Value)).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static double EstimateDirectTravel(string? startKey, RouteNode destination, IEnumerable<RouteNode> nodes)
    {
        if (string.IsNullOrWhiteSpace(startKey)) return 0;
        var start = nodes.FirstOrDefault(x => x.Key.Equals(startKey, StringComparison.OrdinalIgnoreCase));
        if (start is null || (start.X == 0 && start.Y == 0) || (destination.X == 0 && destination.Y == 0)) return 0;
        var dx = destination.X - start.X;
        var dy = destination.Y - start.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        return Math.Max(1, distance / 1_000.0);
    }
}
