using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CudaSpirit.Core.Services.OpenRouter;

/// <summary>
/// Thin client over the OpenRouter chat-completions endpoint. Supports both a buffered call
/// and a token-streamed call (SSE) for the live chat feel in the advisor panel.
/// </summary>
public sealed class OpenRouterClient
{
    private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";

    private readonly HttpClient _http;
    private readonly Func<string> _apiKeyProvider;

    public OpenRouterClient(HttpClient http, Func<string> apiKeyProvider)
    {
        _http = http;
        _apiKeyProvider = apiKeyProvider;
    }

    private HttpRequestMessage BuildRequest(ChatRequest body)
    {
        var key = _apiKeyProvider();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("OpenRouter API key is not set. Add it in Settings.");

        var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        // App attribution without claiming an unowned website.
        req.Headers.TryAddWithoutValidation("X-Title", "Cuda Spirit");

        var json = JsonSerializer.Serialize(body);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return req;
    }

    /// <summary>Single buffered completion. Returns the assistant text.</summary>
    public async Task<string> CompleteAsync(ChatRequest body, CancellationToken ct = default)
    {
        body.Stream = false;
        using var req = BuildRequest(body);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var payload = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new OpenRouterException((int)resp.StatusCode, payload);

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }

    /// <summary>
    /// Streamed completion. Yields text deltas as they arrive (Server-Sent Events).
    /// Consume with <c>await foreach</c>.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest body,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        body.Stream = true;
        using var req = BuildRequest(body);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new OpenRouterException((int)resp.StatusCode, err);
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (line.Length == 0 || !line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = line[5..].Trim();
            if (data == "[DONE]") yield break;

            string? delta = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var choice = doc.RootElement.GetProperty("choices")[0];
                if (choice.TryGetProperty("delta", out var d) &&
                    d.TryGetProperty("content", out var c) &&
                    c.ValueKind == JsonValueKind.String)
                {
                    delta = c.GetString();
                }
            }
            catch (JsonException)
            {
                // OpenRouter interleaves ": OPENROUTER PROCESSING" keep-alive comments; ignore.
                continue;
            }

            if (!string.IsNullOrEmpty(delta))
                yield return delta;
        }
    }
}

public sealed class OpenRouterException : Exception
{
    public int StatusCode { get; }
    public OpenRouterException(int status, string body)
        : base($"OpenRouter request failed ({status}): {Trim(body)}")
        => StatusCode = status;

    private static string Trim(string s) => s.Length > 400 ? s[..400] + "…" : s;
}
