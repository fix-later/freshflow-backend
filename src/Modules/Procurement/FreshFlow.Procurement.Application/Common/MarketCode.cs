using System.Globalization;
using System.Text;

namespace FreshFlow.Procurement.Application.Common;

public static class MarketCode
{
    // ponytail: derived initials can collide; admin Code wins and this is only the MVP fallback.
    public static string DeriveMarketCode(string name)
    {
        var normalized = name
            .Replace('Đ', 'D')
            .Replace('đ', 'd')
            .Normalize(NormalizationForm.FormD);
        var words = new string(normalized
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
                .ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var start = words.Length > 0 && words[0].Equals("Cho", StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;

        return string.Concat(words
            .Skip(start)
            .Take(4)
            .Select(word => char.ToUpperInvariant(word[0])));
    }
}
