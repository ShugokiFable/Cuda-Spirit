using System.Net.Http.Json;
using System.Text.Json;
using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Live;

/// <summary>
/// Fetches publicly available player gear data from Garmoth.com. This mirrors what anyone can see
/// by visiting a player's Garmoth profile page - no private game data is accessed.
///
/// If the player hasn't registered on Garmoth or has their profile set to private, the client
/// returns null gracefully and the app falls back to manual entry.
/// </summary>
public sealed class GarmothClient
{
    private readonly HttpClient _http;

    // Known Garmoth API endpoints (public, no auth required).
    private const string BaseUrl = "https://garmoth.com/api";

    public GarmothClient(HttpClient http) => _http = http;

    /// <summary>
    /// Try to fetch a player's gear overview from Garmoth by family name and region.
    /// Returns null if the player isn't found or the API changed.
    /// </summary>
    public async Task<GarmothProfile?> GetProfileAsync(string familyName, Region region, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(familyName)) return null;

        try
        {
            var regionStr = region switch
            {
                Region.EU => "eu",
                Region.NA => "na",
                Region.SA => "sa",
                Region.MENA => "mena",
                Region.Asia => "sea",
                _ => "na"
            };

            // Try the character/profile endpoint.
            var url = $"{BaseUrl}/character?region={regionStr}&familyName={Uri.EscapeDataString(familyName)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            return ParseGarmothProfile(json, familyName);
        }
        catch
        {
            // Network failure, API changed, etc. - graceful fallback.
            return null;
        }
    }

    private static GarmothProfile? ParseGarmothProfile(string json, string familyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var profile = new GarmothProfile { FamilyName = familyName };

            // Try to extract AP/DP if available.
            if (root.TryGetProperty("ap", out var ap))
                profile.AP = ap.GetInt32();
            if (root.TryGetProperty("dp", out var dp))
                profile.DP = dp.GetInt32();
            if (root.TryGetProperty("apAwakening", out var awk))
                profile.AwakeningAP = awk.GetInt32();

            // Try to extract gear items.
            if (root.TryGetProperty("gear", out var gear) && gear.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in gear.EnumerateArray())
                {
                    var gearItem = new GearItem();
                    if (item.TryGetProperty("name", out var name))
                        gearItem.Name = name.GetString() ?? "";
                    if (item.TryGetProperty("slot", out var slot))
                        gearItem.Slot = ParseSlotEnum(slot.GetString());
                    if (item.TryGetProperty("enhancement", out var enh))
                        gearItem.Grade = ParseGrade(enh.GetString());
                    if (item.TryGetProperty("ap", out var iap))
                        gearItem.Ap = iap.GetInt32();
                    if (item.TryGetProperty("dp", out var idp))
                        gearItem.Dp = idp.GetInt32();
                    if (item.TryGetProperty("caphras", out var cap))
                        gearItem.Caphras = cap.GetInt32();
                    gearItem.Equipped = true;

                    if (!string.IsNullOrEmpty(gearItem.Name))
                        profile.Gear.Add(gearItem);
                }
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static GearSlot ParseSlotEnum(string? s) => s?.ToLowerInvariant() switch
    {
        "main" or "mainhand" or "main-hand" => GearSlot.MainWeapon,
        "off" or "offhand" or "off-hand" or "sub" => GearSlot.Sub,
        "awakening" or "awk" => GearSlot.Awakening,
        "helm" or "helmet" => GearSlot.Helmet,
        "armor" or "chest" => GearSlot.Armor,
        "gloves" => GearSlot.Gloves,
        "boots" or "shoes" => GearSlot.Shoes,
        "neck" or "necklace" => GearSlot.Necklace,
        "ring" or "ring1" or "ring2" => GearSlot.Ring,
        "ear" or "earring" => GearSlot.Earring,
        "belt" => GearSlot.Belt,
        _ => GearSlot.Other
    };

    /// <summary>Resolve the gear kind from the slot for the enhancement simulator.</summary>
    private static EnhanceKind KindFromSlot(GearSlot slot) => slot switch
    {
        GearSlot.MainWeapon or GearSlot.Awakening or GearSlot.Sub => EnhanceKind.Weapon,
        GearSlot.Helmet or GearSlot.Armor or GearSlot.Gloves or GearSlot.Shoes => EnhanceKind.Armor,
        GearSlot.Ring or GearSlot.Earring or GearSlot.Necklace or GearSlot.Belt => EnhanceKind.Accessory,
        _ => EnhanceKind.Weapon
    };

    private static EnhanceGrade ParseGrade(string? s) => s?.ToUpperInvariant() switch
    {
        "I" or "PRI" => EnhanceGrade.PRI,
        "II" or "DUO" => EnhanceGrade.DUO,
        "III" or "TRI" => EnhanceGrade.TRI,
        "IV" or "TET" => EnhanceGrade.TET,
        "V" or "PEN" => EnhanceGrade.PEN,
        _ => EnhanceGrade.Base
    };
}

/// <summary>Public gear profile from Garmoth. Null fields mean the data wasn't available.</summary>
public sealed class GarmothProfile
{
    public string FamilyName { get; set; } = "";
    public int AP { get; set; }
    public int AwakeningAP { get; set; }
    public int DP { get; set; }
    public List<GearItem> Gear { get; set; } = new();
}