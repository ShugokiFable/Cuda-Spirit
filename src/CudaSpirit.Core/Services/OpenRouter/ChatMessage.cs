using System.Text.Json.Serialization;

namespace CudaSpirit.Core.Services.OpenRouter;

public static class ChatRole
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>
/// A chat message. Content is either a plain string or, for vision, an array of content parts.
/// We keep it as <see cref="object"/> so a single type serializes both shapes correctly.
/// </summary>
public sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = ChatRole.User;

    [JsonPropertyName("content")]
    public object Content { get; set; } = "";

    public static ChatMessage System(string text) => new() { Role = ChatRole.System, Content = text };
    public static ChatMessage User(string text) => new() { Role = ChatRole.User, Content = text };
    public static ChatMessage Assistant(string text) => new() { Role = ChatRole.Assistant, Content = text };

    /// <summary>Build a multimodal user message: prompt text + one or more base64 data-URL images.</summary>
    public static ChatMessage Vision(string text, IEnumerable<string> imageDataUrls)
    {
        var parts = new List<object> { new { type = "text", text } };
        foreach (var url in imageDataUrls)
            parts.Add(new { type = "image_url", image_url = new { url } });
        return new ChatMessage { Role = ChatRole.User, Content = parts };
    }
}

public sealed class ChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = new();
    [JsonPropertyName("models")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Models { get; set; }
    [JsonPropertyName("stream")] public bool Stream { get; set; }
    [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.4;
    [JsonPropertyName("max_tokens")] public int? MaxTokens { get; set; }
}
