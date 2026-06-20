using System.Text.Json;
using System.Text.RegularExpressions;

namespace Immo.Parser.Strategies;

/// <summary>
/// Resolves simple dot-notation and array-index paths against a <see cref="JsonElement"/>.
/// <para>
/// Supported syntax examples:
/// <list type="bullet">
///   <item><c>title</c>               — top-level property</item>
///   <item><c>price.amount</c>        — nested property</item>
///   <item><c>photos[0].url</c>       — array index then property</item>
///   <item><c>address.zip</c>         — deeply nested</item>
/// </list>
/// Returns <c>null</c> for any path that does not resolve (missing key, wrong type, out-of-range index).
/// </para>
/// </summary>
public static class JsonPathHelper
{
    private static readonly Regex _indexedSegment = new(@"^(.+?)\[(\d+)\]$", RegexOptions.Compiled);

    /// <summary>Splits a dot-notation path into individual segments, respecting array-index notation.</summary>
    private static string[] Split(string path) => path.Split('.');

    private static JsonElement? Navigate(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return root;

        JsonElement current = root;
        foreach (var rawSegment in Split(path))
        {
            var m = _indexedSegment.Match(rawSegment);
            if (m.Success)
            {
                // e.g. "photos[0]" → navigate into "photos" then take index 0
                var key = m.Groups[1].Value;
                var idx = int.Parse(m.Groups[2].Value);

                if (current.ValueKind != JsonValueKind.Object) return null;
                if (!current.TryGetProperty(key, out var arr)) return null;
                if (arr.ValueKind != JsonValueKind.Array) return null;
                if (idx >= arr.GetArrayLength()) return null;
                current = arr[idx];
            }
            else
            {
                if (current.ValueKind != JsonValueKind.Object) return null;
                if (!current.TryGetProperty(rawSegment, out var next)) return null;
                current = next;
            }
        }

        return current;
    }

    public static string? GetString(JsonElement root, string? path)
    {
        if (path is null) return null;
        var el = Navigate(root, path);
        if (el is null) return null;
        return el.Value.ValueKind switch
        {
            JsonValueKind.String => el.Value.GetString(),
            JsonValueKind.Number => el.Value.GetRawText(),
            JsonValueKind.True   => "true",
            JsonValueKind.False  => "false",
            _                    => null
        };
    }

    public static decimal? GetDecimal(JsonElement root, string? path)
    {
        if (path is null) return null;
        var el = Navigate(root, path);
        if (el is null) return null;
        if (el.Value.ValueKind == JsonValueKind.Number && el.Value.TryGetDecimal(out var d)) return d;
        // Fallback: strip non-numeric characters from a string value
        var text = GetString(root, path);
        if (text is null) return null;
        var digits = Regex.Replace(text, @"[^0-9]", "");
        return decimal.TryParse(digits, out var parsed) ? parsed : null;
    }

    public static int? GetInt(JsonElement root, string? path)
    {
        if (path is null) return null;
        var el = Navigate(root, path);
        if (el is null) return null;
        if (el.Value.ValueKind == JsonValueKind.Number && el.Value.TryGetInt32(out var i)) return i;
        var text = GetString(root, path);
        if (text is null) return null;
        var digits = Regex.Replace(text, @"[^0-9]", "");
        return int.TryParse(digits, out var parsed) ? parsed : null;
    }

    public static double? GetDouble(JsonElement root, string? path)
    {
        if (path is null) return null;
        var el = Navigate(root, path);
        if (el is null) return null;
        if (el.Value.ValueKind == JsonValueKind.Number && el.Value.TryGetDouble(out var d)) return d;
        var text = GetString(root, path);
        if (text is null) return null;
        var digits = Regex.Replace(text, @"[^0-9]", "");
        return double.TryParse(digits, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Navigates to <paramref name="arrayPath"/> and returns the resulting <see cref="JsonElement"/> array.
    /// If <paramref name="arrayPath"/> is null/empty the root element itself is treated as the array.
    /// Returns an empty enumerable on any mismatch.
    /// </summary>
    public static IEnumerable<JsonElement> GetArray(JsonElement root, string? arrayPath)
    {
        JsonElement target = root;
        if (!string.IsNullOrWhiteSpace(arrayPath))
        {
            var el = Navigate(root, arrayPath);
            if (el is null) yield break;
            target = el.Value;
        }

        if (target.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in target.EnumerateArray()) yield return item;
    }
}
