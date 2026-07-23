using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.OpenRouter;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.Core.Services.Ai;

/// <summary>
/// The advisor brain. Wraps <see cref="OpenRouterClient"/> with:
///  - automatic system-prompt injection via <see cref="ContextAggregator"/>,
///  - model routing (background / reasoning / vision),
///  - a SQLite response cache keyed by (model + system + prompt) to cut API cost,
///  - streaming for the live chat feel.
/// </summary>
public sealed class AdvisorService
{
    private readonly OpenRouterClient _client;
    private readonly ContextAggregator _context;
    private readonly SettingsService _settings;
    private readonly AppDatabase _db;

    public AdvisorService(
        OpenRouterClient client,
        ContextAggregator context,
        SettingsService settings,
        AppDatabase db)
    {
        _client = client;
        _context = context;
        _settings = settings;
        _db = db;
    }

    private string ModelFor(AiTaskKind kind) => _settings.Current.Models.Resolve(kind);

    /// <summary>
    /// Buffered ask. Uses the cache for non-vision reasoning/background tasks (vision is never cached
    /// because the image content changes every time).
    /// </summary>
    public async Task<string> AskAsync(
        string userPrompt,
        AiTaskKind kind = AiTaskKind.Reasoning,
        bool useCache = true,
        CancellationToken ct = default)
    {
        var model = ModelFor(kind);
        var system = _context.BuildSystemPrompt(userPrompt);

        if (useCache && kind != AiTaskKind.Vision)
        {
            var key = CacheKey(model, system, userPrompt);
            var hit = _db.GetCachedAi(key, TimeSpan.FromMinutes(_settings.Current.AiCacheMinutes));
            if (hit is not null) return hit;

            var fresh = await _client.CompleteAsync(BuildRequest(model, system, userPrompt, kind), ct);
            _db.PutCachedAi(key, model, fresh);
            return fresh;
        }

        return await _client.CompleteAsync(BuildRequest(model, system, userPrompt, kind), ct);
    }

    /// <summary>Streamed ask for the chat panel. Yields text deltas; also caches the full result.</summary>
    public async IAsyncEnumerable<string> StreamAsync(
        string userPrompt,
        AiTaskKind kind = AiTaskKind.Reasoning,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var model = ModelFor(kind);
        var system = _context.BuildSystemPrompt(userPrompt);
        var full = new StringBuilder();

        await foreach (var delta in _client.StreamAsync(BuildRequest(model, system, userPrompt, kind), ct))
        {
            full.Append(delta);
            yield return delta;
        }

        if (kind != AiTaskKind.Vision && full.Length > 0)
            _db.PutCachedAi(CacheKey(model, system, userPrompt), model, full.ToString());
    }

    /// <summary>
    /// Vision ask - the manual "read my screen" button. Feeds one or more base64 data-URL images
    /// (e.g. a gear/crystal screenshot) plus a prompt to a vision model. Not cached.
    /// </summary>
    public async Task<string> AskWithImagesAsync(
        string userPrompt,
        IReadOnlyList<string> imageDataUrls,
        CancellationToken ct = default)
    {
        var model = ModelFor(AiTaskKind.Vision);
        var system = _context.BuildSystemPrompt(userPrompt);
        var req = new ChatRequest
        {
            Model = model,
            Models = FallbackModels(AiTaskKind.Vision),
            Messages =
            {
                ChatMessage.System(system),
                ChatMessage.Vision(userPrompt, imageDataUrls)
            },
            Temperature = 0.2
        };
        return await _client.CompleteAsync(req, ct);
    }

    private ChatRequest BuildRequest(string model, string system, string user, AiTaskKind kind) => new()
    {
        Model = model,
        Models = FallbackModels(kind),
        Messages =
        {
            ChatMessage.System(system),
            ChatMessage.User(user)
        }
    };

    private List<string>? FallbackModels(AiTaskKind kind)
    {
        var models = _settings.Current.Models.Fallbacks(kind).ToList();
        return models.Count == 0 ? null : models;
    }

    private static string CacheKey(string model, string system, string user)
    {
        var raw = $"{model}{system}{user}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
