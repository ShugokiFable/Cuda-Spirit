using System.Text.Json;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;

namespace CudaSpirit.Core.Services.Market;

/// <summary>
/// Read-only Central Market data client. Backed by the public arsha.io v2 API, which mirrors the
/// official Black Desert Central Market feed as plain JSON. This is a *data* endpoint - it does not
/// touch the game client, its memory, or its network traffic, and it cannot place orders. Buying and
/// selling always happens by the player, in-game.
///
/// Swap <see cref="BaseUrl"/> to an official region trade host if you prefer that source.
/// </summary>
public sealed class BdoMarketClient
{
    public string BaseUrl { get; set; } = "https://api.arsha.io/v2";

    private readonly HttpClient _http;
    private readonly AppDatabase _db;
    private readonly TimeSpan _cacheTtl;

    public BdoMarketClient(HttpClient http, AppDatabase db, TimeSpan? cacheTtl = null)
    {
        _http = http;
        _db = db;
        _cacheTtl = cacheTtl ?? TimeSpan.FromMinutes(10);
    }

    private static string RegionCode(Region r) => r switch
    {
        Region.NA => "na",
        Region.EU => "eu",
        Region.SA => "sa",
        Region.MENA => "mena",
        Region.Asia => "sea",
        Region.Console => "console_na",
        _ => "na"
    };

    /// <summary>Fetch a single item's current price/stock, using the SQLite cache when fresh.</summary>
    public async Task<MarketItem?> GetItemAsync(long itemId, int sid, Region region, CancellationToken ct = default)
    {
        var cached = _db.GetCachedMarket(itemId, sid, _cacheTtl);
        if (cached is not null) return cached;
        var stale = _db.GetCachedMarket(itemId, sid, TimeSpan.Zero, allowStale: true);

        var url = $"{BaseUrl}/{RegionCode(region)}/item?id={itemId}&sid={sid}&lang=en";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return stale; // fall back to any stale cache
        var json = await resp.Content.ReadAsStringAsync(ct);

        var item = ParseItem(json, itemId, sid);
        if (item is not null) _db.CacheMarket(item);
        return item;
    }

    private static MarketItem? ParseItem(string json, long itemId, int sid)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // The API returns an array for multi-sid items or a single object.
            var el = root.ValueKind == JsonValueKind.Array
                ? FirstMatchingSid(root, sid)
                : root;
            if (el is null) return null;
            var e = el.Value;

            return new MarketItem
            {
                ItemId = GetLong(e, "id", itemId),
                Sid = (int)GetLong(e, "sid", sid),
                Name = GetString(e, "name") ?? $"Item {itemId}",
                Grade = (int)GetLong(e, "grade", 0),
                BasePrice = GetLong(e, "basePrice", GetLong(e, "lastSoldPrice", 0)),
                CurrentStock = GetLong(e, "currentStock", 0),
                TotalTrades = GetLong(e, "totalTrades", 0),
                Retrieved = DateTimeOffset.UtcNow
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? FirstMatchingSid(JsonElement arr, int sid)
    {
        foreach (var e in arr.EnumerateArray())
            if (GetLong(e, "sid", -1) == sid) return e;
        return arr.GetArrayLength() > 0 ? arr[0] : null;
    }

    /// <summary>Daily price history (used for the chart).</summary>
    public async Task<IReadOnlyList<PricePoint>> GetHistoryAsync(long itemId, int sid, Region region, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/{RegionCode(region)}/history?id={itemId}&sid={sid}&lang=en";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return Array.Empty<PricePoint>();
        var json = await resp.Content.ReadAsStringAsync(ct);

        var points = new List<PricePoint>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
                ? doc.RootElement[0]
                : doc.RootElement;

            if (root.TryGetProperty("history", out var hist) && hist.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in hist.EnumerateObject())
                {
                    if (long.TryParse(prop.Name, out var epochMs) && prop.Value.TryGetInt64(out var price))
                        points.Add(new PricePoint(DateTimeOffset.FromUnixTimeMilliseconds(epochMs), price));
                }
            }
        }
        catch (JsonException) { /* return what we parsed */ }

        points.Sort((a, b) => a.Date.CompareTo(b.Date));
        return points;
    }

    // ---- small JSON helpers ----------------------------------------------

    private static long GetLong(JsonElement e, string name, long fallback)
        => e.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : fallback;

    private static string? GetString(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
