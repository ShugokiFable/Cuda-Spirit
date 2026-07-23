using System.Text.Json;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.LiveData;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>
/// Optional keyed source for regional boss, reset, news, maintenance, coupon, and market data
/// from BDO Alerts. Endpoints are isolated so one unavailable feed does not discard the rest.
/// </summary>
public sealed class BdoAlertsSource
{
    private readonly HttpClient _http;
    private readonly AppDatabase _db;

    public BdoAlertsSource(HttpClient http, AppDatabase db)
    {
        _http = http;
        _db = db;
    }

    public string BaseUrl { get; set; } = "https://api.bdoalerts.net";

    public async Task<IReadOnlyList<KnowledgeRecord>> FetchAsync(string apiKey, Region region, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return Array.Empty<KnowledgeRecord>();

        var headers = new Dictionary<string, string> { ["X-API-Key"] = apiKey.Trim() };
        var code = RegionCode(region);
        var result = new List<KnowledgeRecord>();
        var errors = new List<string>();

        var calls = new List<EndpointCall>
        {
            new($"/api/boss-timers/{code}", KnowledgeKinds.Boss, $"boss:{code}", $"{code.ToUpperInvariant()} boss timers", TimeSpan.FromMinutes(3)),
            new($"/api/reset-timers?region={code}", KnowledgeKinds.Reset, $"reset:{code}", $"{code.ToUpperInvariant()} reset timers", TimeSpan.FromMinutes(3)),
            new("/api/news?board_type=2&limit=20", KnowledgeKinds.News, "news:latest", "Latest Black Desert news", TimeSpan.FromMinutes(10), "global"),
            new($"/api/maintenance-status?region={code}", KnowledgeKinds.Maintenance, $"maintenance:{code}", $"{code.ToUpperInvariant()} maintenance status", TimeSpan.FromMinutes(2)),
            new("/api/coupons", KnowledgeKinds.Coupon, "coupons:active", "Active Black Desert coupons", TimeSpan.FromMinutes(15), "global")
        };

        // BDO Alerts currently documents market coverage for PC NA/EU only.
        if (code is "na" or "eu")
            calls.Add(new EndpointCall($"/api/market/{code}/hot?limit=100", KnowledgeKinds.Market, $"hot:{code}", $"{code.ToUpperInvariant()} hot market", TimeSpan.FromMinutes(35)));

        foreach (var call in calls)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = await HttpFetch.GetStringAsync(_http, BaseUrl + call.Path, ct, headers);
                var now = DateTimeOffset.UtcNow;
                result.Add(new KnowledgeRecord
                {
                    SourceId = "bdo-alerts",
                    ExternalId = call.ExternalId,
                    Kind = call.Kind,
                    Title = call.Title,
                    Summary = SummarizeJson(json),
                    Content = KnowledgeText.Truncate(json, 40_000),
                    Url = BaseUrl + call.Path,
                    Region = call.Region ?? code,
                    Tags = $"live {call.Kind} regional bdo-alerts",
                    RetrievedAt = now,
                    ExpiresAt = now.Add(call.Ttl),
                    Confidence = 0.95,
                    ContentHash = KnowledgeText.Hash(json)
                });

                if (call.Kind == KnowledgeKinds.Market)
                    foreach (var item in MarketPayloadParser.ParseMany(json, 200))
                        _db.CacheMarket(item, "bdo-alerts");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                errors.Add($"{call.Kind}: {ex.Message}");
            }
        }

        if (result.Count == 0 && errors.Count > 0)
            throw new InvalidOperationException("Every BDO Alerts endpoint failed: " + string.Join(" | ", errors.Take(4)));

        return result;
    }

    private static string RegionCode(Region region) => region switch
    {
        Region.NA => "na",
        Region.EU => "eu",
        Region.SA => "sa",
        Region.Console => "console_na",
        Region.Asia or Region.MENA => "asia",
        _ => "na"
    };

    private static string SummarizeJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => $"Live payload containing {doc.RootElement.GetArrayLength()} entries.",
                JsonValueKind.Object => $"Live payload containing {doc.RootElement.EnumerateObject().Count()} fields.",
                _ => "Live regional data payload."
            };
        }
        catch (JsonException)
        {
            return "Live regional data payload.";
        }
    }

    private sealed record EndpointCall(
        string Path,
        string Kind,
        string ExternalId,
        string Title,
        TimeSpan Ttl,
        string? Region = null);
}
