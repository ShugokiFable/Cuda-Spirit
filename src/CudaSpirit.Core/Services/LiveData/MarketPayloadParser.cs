using System.Text.Json;
using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.LiveData;

internal static class MarketPayloadParser
{
    public static IReadOnlyList<MarketItem> ParseMany(string json, int max = 500)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<MarketItem>();
        Walk(doc.RootElement, result, max);
        return result
            .GroupBy(x => (x.ItemId, x.Sid))
            .Select(g => g.Last())
            .Take(max)
            .ToList();
    }

    private static void Walk(JsonElement element, List<MarketItem> result, int max)
    {
        if (result.Count >= max) return;
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray()) Walk(child, result, max);
                break;
            case JsonValueKind.Object:
                if (TryReadItem(element, out var item)) result.Add(item);
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                        Walk(prop.Value, result, max);
                }
                break;
        }
    }

    private static bool TryReadItem(JsonElement e, out MarketItem item)
    {
        item = new MarketItem();
        var id = GetLong(e, "id", GetLong(e, "itemId", GetLong(e, "mainKey", 0)));
        var price = GetLong(e, "basePrice", GetLong(e, "lastSoldPrice", GetLong(e, "price", 0)));
        if (id <= 0 || price < 0) return false;

        item = new MarketItem
        {
            ItemId = id,
            Sid = (int)GetLong(e, "sid", GetLong(e, "subKey", 0)),
            Name = GetString(e, "name") ?? GetString(e, "itemName") ?? $"Item {id}",
            Grade = (int)GetLong(e, "grade", 0),
            BasePrice = price,
            CurrentStock = GetLong(e, "currentStock", GetLong(e, "stock", GetLong(e, "count", 0))),
            TotalTrades = GetLong(e, "totalTrades", GetLong(e, "totalTradeCount", GetLong(e, "trades", 0))),
            Retrieved = DateTimeOffset.UtcNow
        };
        return true;
    }

    private static long GetLong(JsonElement e, string name, long fallback)
    {
        if (!TryProperty(e, name, out var p)) return fallback;
        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetInt64(out var v) => v,
            JsonValueKind.String when long.TryParse(p.GetString(), out var v) => v,
            _ => fallback
        };
    }

    private static string? GetString(JsonElement e, string name)
    {
        if (!TryProperty(e, name, out var p)) return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    private static bool TryProperty(JsonElement e, string name, out JsonElement value)
    {
        if (e.TryGetProperty(name, out value)) return true;
        foreach (var prop in e.EnumerateObject())
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        value = default;
        return false;
    }
}
