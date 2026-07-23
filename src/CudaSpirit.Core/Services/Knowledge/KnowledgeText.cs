using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CudaSpirit.Core.Services.Knowledge;

internal static partial class KnowledgeText
{
    [GeneratedRegex("<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex SpaceRegex();

    public static string CleanHtml(string? html, int maxLength = 24_000)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = ScriptStyleRegex().Replace(html, " ");
        text = TagRegex().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = SpaceRegex().Replace(text, " ").Trim();
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    public static string Hash(params string?[] values)
    {
        var raw = string.Join('\u001f', values.Select(x => x ?? ""));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    public static string Slug(string value)
    {
        var chars = value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return Regex.Replace(new string(chars), "-+", "-").Trim('-');
    }

    public static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
