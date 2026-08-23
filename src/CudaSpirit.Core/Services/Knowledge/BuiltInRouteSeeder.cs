using System.IO;
using System.Text.Json;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>
/// Seeds the route graph with a bundled, sourced catalog of real Black Desert grind zones
/// (data/bdo-grind-catalog.json - zone stats from the GrumpyG monster-zone table, Aug 2024 update,
/// current roster as of Aug 2026). Runs once per catalog version: a schema_info marker records the
/// seeded version, so user-imported edits after the seed are never overwritten. Local imports and
/// live syncs remain the fresher layer on top.
/// </summary>
public sealed class BuiltInRouteSeeder
{
    public const string CatalogVersion = "2026.08-r1";
    private const string MarkerKey = "route_seed_version";

    private readonly AppDatabase _db;

    public BuiltInRouteSeeder(AppDatabase db) => _db = db;

    /// <summary>Seed the bundled catalog when its version hasn't been seeded yet. Returns nodes added.</summary>
    public int Seed()
    {
        var seeded = _db.GetSchemaInfo(MarkerKey);
        if (seeded == CatalogVersion) return 0;

        var json = ReadCatalog();
        if (string.IsNullOrWhiteSpace(json)) return 0;

        var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (!doc.RootElement.TryGetProperty("nodes", out var nodesEl)) return 0;

        var source = "cuda-bdo-catalog-" + CatalogVersion;
        var nodes = new List<RouteNode>();
        foreach (var obj in nodesEl.EnumerateArray())
        {
            var node = new RouteNode
            {
                Key = Str(obj, "key"),
                Name = Str(obj, "name"),
                Territory = Str(obj, "territory"),
                RecommendedAp = (int)Num(obj, "recommendedAp"),
                RecommendedDp = (int)Num(obj, "recommendedDp"),
                ExpectedSilverPerHour = (long)Num(obj, "expectedSilverPerHour"),
                Risk = Num(obj, "risk"),
                Tags = Str(obj, "tags"),
                X = Num(obj, "x"),
                Y = Num(obj, "y"),
                SourceId = source,
                UpdatedAt = DateTimeOffset.Parse(Str(obj, "updatedAt"))
            };
            if (!string.IsNullOrWhiteSpace(node.Key) && !string.IsNullOrWhiteSpace(node.Name))
                nodes.Add(node);
        }

        var edges = new List<RouteEdge>();
        if (doc.RootElement.TryGetProperty("edges", out var edgesEl))
        {
            foreach (var obj in edgesEl.EnumerateArray())
            {
                var edge = new RouteEdge
                {
                    FromKey = Str(obj, "from"),
                    ToKey = Str(obj, "to"),
                    TravelMinutes = Num(obj, "travelMinutes"),
                    Risk = Num(obj, "risk"),
                    Bidirectional = !obj.TryGetProperty("bidirectional", out var bi) || bi.GetBoolean(),
                    Transport = obj.TryGetProperty("transport", out var tr) ? tr.GetString() ?? "ground" : "ground",
                    SourceId = source,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                if (edge.TravelMinutes > 0)
                    edges.Add(edge);
            }
        }

        var added = _db.UpsertRouteNodes(nodes);
        _db.UpsertRouteEdges(edges);
        _db.SetSchemaInfo(MarkerKey, CatalogVersion);
        return added;
    }

    private static string ReadCatalog()
    {
        // Embedded resource ships inside the exe; a loose file next to it (or in the repo layout)
        // wins so the catalog can be updated without recompiling.
        var asm = typeof(BuiltInRouteSeeder).Assembly;
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("bdo-grind-catalog.json", StringComparison.OrdinalIgnoreCase));
        if (resName is not null)
            using (var s = asm.GetManifestResourceStream(resName)!)
                using (var r = new StreamReader(s))
                    return r.ReadToEnd();

        var probe = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "bdo-grind-catalog.json"),
            Path.Combine(AppContext.BaseDirectory, "bdo-grind-catalog.json")
        };
        return probe.FirstOrDefault(File.Exists) is { } path ? File.ReadAllText(path) : "";
    }

    private static string Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

    private static double Num(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.TryGetDouble(out var d) ? d : 0;
}
