using System.Text;

namespace PeriphShop.Application.Common;

/// <summary>Формирование ЧПУ-адресов из русских и латинских названий (FR-02).</summary>
public static class Slug
{
    private static readonly Dictionary<char, string> Translit = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e",
        ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
        ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "h", ['ц'] = "c", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "",
        ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
    };

    public static string From(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;

        var sb = new StringBuilder(source.Length);
        foreach (var raw in source.Trim().ToLowerInvariant())
        {
            if (Translit.TryGetValue(raw, out var replacement)) sb.Append(replacement);
            else if (char.IsLetterOrDigit(raw) && raw < 128) sb.Append(raw);
            else if (raw is ' ' or '-' or '_' or '.' or '/') sb.Append('-');
        }

        var result = sb.ToString();
        while (result.Contains("--")) result = result.Replace("--", "-");
        return result.Trim('-');
    }

    /// <summary>Возвращает уникальный slug, добавляя числовой суффикс при коллизии.</summary>
    public static string Unique(string source, Func<string, bool> exists)
    {
        var basis = From(source);
        if (string.IsNullOrEmpty(basis)) basis = "item";

        var candidate = basis;
        var suffix = 2;
        while (exists(candidate))
        {
            candidate = $"{basis}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
