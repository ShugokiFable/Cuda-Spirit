using CudaSpirit.Core.Services.LiveData;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>
/// Fetches public official pages with HttpClient first and a serialized rendered-browser fallback
/// when Pearl Abyss serves an anti-bot challenge. The fallback is supplied by the desktop host.
/// </summary>
internal sealed class OfficialPageFetcher
{
    private readonly HttpClient _http;
    private readonly Func<string, CancellationToken, Task<string>>? _renderedFallback;
    private readonly SemaphoreSlim _browserGate = new(1, 1);

    public OfficialPageFetcher(HttpClient http, Func<string, CancellationToken, Task<string>>? renderedFallback = null)
    {
        _http = http;
        _renderedFallback = renderedFallback;
    }

    public async Task<string> GetAsync(string url, CancellationToken ct, int attempts = 3)
    {
        Exception? original = null;
        try
        {
            var html = await HttpFetch.GetStringAsync(_http, url, ct, attempts: attempts);
            if (!LooksBlocked(html)) return html;
            original = new HttpRequestException("Official page returned a browser challenge instead of content.");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            original = ex;
        }

        if (_renderedFallback is null) throw original ?? new HttpRequestException("Official page unavailable.");

        await _browserGate.WaitAsync(ct);
        try
        {
            var rendered = await _renderedFallback(url, ct);
            if (LooksBlocked(rendered)) throw original ?? new HttpRequestException("Rendered official page remained blocked.");
            return rendered;
        }
        finally
        {
            _browserGate.Release();
        }
    }

    private static bool LooksBlocked(string html) => string.IsNullOrWhiteSpace(html) || html.Length < 1_000 ||
        html.Contains("Incapsula incident", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("Request unsuccessful", StringComparison.OrdinalIgnoreCase) ||
        html.Contains("_Incapsula_Resource", StringComparison.OrdinalIgnoreCase);
}
