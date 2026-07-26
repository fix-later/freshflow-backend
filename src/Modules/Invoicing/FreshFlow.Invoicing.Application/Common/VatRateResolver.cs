namespace FreshFlow.Invoicing.Application.Common;

/// <summary>
/// Resolves a product's stored VAT rate code to a numeric percent for invoice math. Fresh food is
/// not uniformly 10% — many items are 5% or không chịu thuế (KCT), so the rate is data-driven off
/// the product, never hardcoded. Unknown/unset codes resolve to 0 (treated as KCT) for v1; the
/// definitive per-category rates are a go-live decision (see blueprint §9).
/// </summary>
public static class VatRateResolver
{
    public const string CodeKct = "KCT";

    // ponytail: null/unknown → 0% (KCT). Tighten to fail-closed once per-category rates are signed off.
    public static decimal ToPercent(string? code) => Normalize(code) switch
    {
        CodeKct or "0" => 0m,
        "5" => 5m,
        "8" => 8m,
        "10" => 10m,
        _ => 0m
    };

    public static string Normalize(string? code) =>
        string.IsNullOrWhiteSpace(code) ? CodeKct : code.Trim().ToUpperInvariant();
}
