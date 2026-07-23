using System.Text.RegularExpressions;
using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Live;

/// <summary>
/// Fetches a player's PUBLIC adventurer profile from the official Black Desert website and parses
/// the visible stats (gear score, class, level, region, CP, energy, guild). The player opted into
/// showing this page; it is public web data, not anything read from the game process.
///
/// Usage: the user pastes their profile link (or the raw profileTarget token) once; the app stores
/// the token and re-fetches on each launch so gear score / CP stay current with no manual typing.
///
/// The HTML markup below was verified against a live NA/EU profile page. If Pearl Abyss changes the
/// markup, <see cref="TryParse"/> returns what it can and the caller falls back to manual entry.
/// </summary>
public sealed class BdoProfileClient
{
    private readonly HttpClient _http;

    public BdoProfileClient(HttpClient http) => _http = http;

    /// <summary>Region-specific host for the official profile site.</summary>
    private static string Host(Region region) => region switch
    {
        Region.EU => "www.naeu.playblackdesert.com",   // NA and EU share the naeu host
        Region.NA => "www.naeu.playblackdesert.com",
        Region.SA => "www.sa.playblackdesert.com",
        Region.MENA => "www.tr.playblackdesert.com",
        Region.Asia => "www.sea.playblackdesert.com",
        Region.Console => "www.naeu.playblackdesert.com",
        _ => "www.naeu.playblackdesert.com"
    };

    /// <summary>
    /// Accepts either a full profile URL (…/Adventure/Profile?profileTarget=XXXX) or a bare token,
    /// and returns just the token. Returns null if nothing usable is found.
    /// </summary>
    public static string? ExtractToken(string urlOrToken)
    {
        if (string.IsNullOrWhiteSpace(urlOrToken)) return null;
        urlOrToken = urlOrToken.Trim();

        // Full URL with a profileTarget query parameter (handles both profileTarget and _profileTarget).
        var m = Regex.Match(urlOrToken, @"[?&_]?profileTarget=([^&\s]+)", RegexOptions.IgnoreCase);
        if (m.Success)
            // Decode %XX (e.g. %2F -> '/') but keep '+' as a literal '+' - it is part of the base64
            // token, not a space. Uri.UnescapeDataString does exactly this; UrlDecode would corrupt it.
            return Uri.UnescapeDataString(m.Groups[1].Value);

        // Looks like a bare token already (base64-ish, no spaces).
        if (!urlOrToken.Contains(' ') && !urlOrToken.Contains("http", StringComparison.OrdinalIgnoreCase))
            return urlOrToken;

        return null;
    }

    /// <summary>The official profile URL for a token + region.</summary>
    public static string BuildProfileUrl(string token, Region region) =>
        $"https://{Host(region)}/Adventure/Profile?profileTarget={Uri.EscapeDataString(token)}";

    /// <summary>Fetch and parse a profile by its token. Throws on network/HTTP failure.</summary>
    public async Task<ProfileInfo> GetProfileAsync(string token, Region region, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Profile token is empty.", nameof(token));

        var url = $"https://{Host(region)}/Adventure/Profile?profileTarget={Uri.EscapeDataString(token)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        // Present as a normal browser; the site 200s for this but rejects odd clients.
        req.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
        req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        req.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync(ct);

        var info = TryParse(html);
        if (string.IsNullOrEmpty(info.FamilyName))
            throw new InvalidOperationException(
                "Profile page loaded but no data could be read. The profile may be private, or the page layout changed.");
        info.Region = region;
        info.ProfileToken = token;
        return info;
    }

    /// <summary>Parse the profile HTML. Tolerant: missing fields stay at their defaults.</summary>
    public static ProfileInfo TryParse(string html)
    {
        var info = new ProfileInfo
        {
            FamilyName = Group(html, @"class=""nick"">\s*([^<]+?)\s*(?:<|</p>)") ?? "",
            Guild = Group(html, @"class=""desc guild"">\s*<a[^>]*>\s*([^<]+?)\s*</a>"),
            GearScore = Stat(html, "Max Gear Score"),
            Energy = Stat(html, "Energy"),
            ContributionPoints = Stat(html, "Contribution Points"),
        };

        // Region: <span class="region_info na">NA</span>
        var region = Group(html, @"class=""region_info[^""]*"">\s*([A-Za-z]+)\s*</span>");
        if (!string.IsNullOrEmpty(region) && Enum.TryParse<Region>(region, ignoreCase: true, out var r))
            info.Region = r;

        // Main character block: name … <span class="selected_label">Main Character</span> … <em>CLASS</em> … Lv<em>LEVEL</em>
        var main = Regex.Match(html,
            @"class=""character_name"">\s*([^<]+?)\s*<span class=""selected_label"">Main Character</span>.*?<span class=""character_symbol"">.*?<em[^>]*>\s*</em>\s*<em>\s*([^<]+?)\s*</em>.*?Lv<em>\s*(\d+)\s*</em>",
            RegexOptions.Singleline);
        if (main.Success)
        {
            info.MainCharacterName = main.Groups[1].Value.Trim();
            info.MainClass = main.Groups[2].Value.Trim();
            info.MainLevel = int.TryParse(main.Groups[3].Value, out var lvl) ? lvl : 0;
        }

        return info;
    }

    /// <summary>Read a numbered stat by its title label (e.g. "Max Gear Score" → 583).</summary>
    private static int Stat(string html, string label)
    {
        // Find the title span for the label, then the first following <span class="desc">NUMBER</span>.
        var m = Regex.Match(html,
            $@"class=""title""[^>]*>(?:\s*<span[^>]*></span>)?\s*{Regex.Escape(label)}\s*</span>\s*<span class=""desc"">\s*([\d,]+)",
            RegexOptions.Singleline);
        if (!m.Success) return 0;
        return int.TryParse(m.Groups[1].Value.Replace(",", ""), out var n) ? n : 0;
    }

    private static string? Group(string html, string pattern)
    {
        var m = Regex.Match(html, pattern, RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }
}
