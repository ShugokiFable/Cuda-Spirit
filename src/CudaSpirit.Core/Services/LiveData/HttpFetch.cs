using System.Net;

namespace CudaSpirit.Core.Services.LiveData;

internal static class HttpFetch
{
    public static async Task<string> GetStringAsync(
        HttpClient http,
        string url,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? headers = null,
        int attempts = 3)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("User-Agent", "CudaSpirit/2.4.1");
                req.Headers.TryAddWithoutValidation("Accept", "application/json,text/html;q=0.9,*/*;q=0.7");
                if (headers is not null)
                    foreach (var pair in headers)
                        req.Headers.TryAddWithoutValidation(pair.Key, pair.Value);

                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (resp.IsSuccessStatusCode)
                    return await resp.Content.ReadAsStringAsync(ct);

                var body = await resp.Content.ReadAsStringAsync(ct);
                if (resp.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                {
                    last = new HttpRequestException($"HTTP {(int)resp.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
                }
                else
                {
                    throw new HttpRequestException($"HTTP {(int)resp.StatusCode}: {body[..Math.Min(body.Length, 500)]}");
                }
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
            {
                last = ex;
            }

            if (attempt < attempts)
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt * attempt), ct);
        }

        throw last ?? new HttpRequestException("The remote source did not return data.");
    }
}
