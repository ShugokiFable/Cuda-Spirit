using System.Text.Json;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.LiveData;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>Ingests a bounded market hot list from Arsha.io into both market and knowledge tables.</summary>
public sealed class ArshaLiveSource
{
    private readonly HttpClient _http;
    private readonly AppDatabase _db;

    public ArshaLiveSource(HttpClient http, AppDatabase db)
    {
        _http = http;
        _db = db;
    }

    public string BaseUrl { get; set; } = "https://api.arsha.io/v2";

    public async Task<IReadOnlyList<KnowledgeRecord>> FetchHotAsync(Region region, int limit = 100, CancellationToken ct = default)
    {
        if (region == Region.Console) return Array.Empty<KnowledgeRecord>();

        var code = RegionCode(region);
        var url = $"{BaseUrl}/{code}/hot?lang=en";
        var json = await HttpFetch.GetStringAsync(_http, url, ct);
        var items = MarketPayloadParser.ParseMany(json, Math.Clamp(limit, 1, 500));
        var records = new List<KnowledgeRecord>(items.Count);
        foreach (var item in items)
        {
            _db.CacheMarket(item, "arsha");
            var content = JsonSerializer.Serialize(new
            {
                item.ItemId,
                item.Sid,
                item.Name,
                item.Grade,
                item.BasePrice,
                item.CurrentStock,
                item.TotalTrades,
                observedAt = item.Retrieved
            });
            records.Add(new KnowledgeRecord
            {
                SourceId = "arsha-market",
                ExternalId = $"{code}:{item.ItemId}:{item.Sid}",
                Kind = KnowledgeKinds.Market,
                Title = item.Name,
                Summary = $"Market {item.BasePrice:N0} silver; stock {item.CurrentStock:N0}; trades {item.TotalTrades:N0}.",
                Content = content,
                Url = $"{BaseUrl}/{code}/item?id={item.ItemId}&sid={item.Sid}&lang=en",
                Region = code,
                Tags = $"market hot item {item.ItemId} sid {item.Sid}",
                RetrievedAt = item.Retrieved,
                ExpiresAt = item.Retrieved.AddHours(2),
                Confidence = 0.95,
                ContentHash = KnowledgeText.Hash(content)
            });
        }
        return records;
    }

    public static string RegionCode(Region region) => region switch
    {
        Region.NA => "na",
        Region.EU => "eu",
        Region.SA => "sa",
        Region.MENA => "mena",
        Region.Asia => "sea",
        _ => "na"
    };
}
