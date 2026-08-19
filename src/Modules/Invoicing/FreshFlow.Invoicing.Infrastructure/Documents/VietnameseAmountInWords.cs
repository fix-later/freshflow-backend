namespace FreshFlow.Invoicing.Infrastructure.Documents;

/// <summary>
/// Reads a đồng amount out in Vietnamese for the "Số tiền viết bằng chữ" line of a VAT invoice.
/// Amounts are rounded to whole đồng first — invoices are never worded with decimals.
/// </summary>
public static class VietnameseAmountInWords
{
    private static readonly string[] Digits =
        ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    private static readonly string[] Scales = ["", " nghìn", " triệu"];

    public static string Convert(decimal amount)
    {
        var value = (long)decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        var negative = value < 0;
        value = Math.Abs(value);

        if (value == 0)
            return "Không đồng";

        var groups = new List<int>();
        while (value > 0)
        {
            groups.Add((int)(value % 1000));
            value /= 1000;
        }

        var parts = new List<string>();
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            if (groups[i] == 0)
                continue;
            parts.Add(ReadGroup(groups[i], readLeadingHundred: parts.Count > 0) + ScaleOf(i));
        }

        var words = string.Join(" ", parts) + " đồng";
        if (negative)
            words = "Âm " + words;
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    // ponytail: groups above 10^12 read as "<n> nghìn tỷ" / "<n> triệu tỷ" — enough for any invoice.
    private static string ScaleOf(int groupIndex) =>
        groupIndex < 3 ? Scales[groupIndex] : Scales[groupIndex % 3] + " tỷ";

    private static string ReadGroup(int group, bool readLeadingHundred)
    {
        var hundreds = group / 100;
        var tens = group / 10 % 10;
        var units = group % 10;
        var words = new List<string>();

        if (hundreds > 0 || readLeadingHundred)
            words.Add($"{Digits[hundreds]} trăm");

        if (tens == 0 && units > 0 && words.Count > 0)
            words.Add("lẻ");
        else if (tens == 1)
            words.Add("mười");
        else if (tens > 1)
            words.Add($"{Digits[tens]} mươi");

        if (units == 1 && tens >= 2)
            words.Add("mốt");
        else if (units == 5 && tens >= 1)
            words.Add("lăm");
        else if (units > 0)
            words.Add(Digits[units]);

        return string.Join(" ", words);
    }
}
