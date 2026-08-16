namespace FreshFlow.Invoicing.Domain.Validation;

public static class VietnameseTaxCodeValidator
{
    public static bool IsValid(string? taxCode) =>
        taxCode is { Length: 10 } && AllAsciiDigits(taxCode) ||
        taxCode is { Length: 14 } &&
        taxCode[10] == '-' &&
        AllAsciiDigits(taxCode[..10]) &&
        AllAsciiDigits(taxCode[11..]);

    private static bool AllAsciiDigits(string value) =>
        value.All(character => character is >= '0' and <= '9');
}

public static class InvoiceBuyerValidator
{
    public static string? GetErrorCode(string? taxCode, string? legalName, string? address)
    {
        if (string.IsNullOrWhiteSpace(taxCode))
            return "BUYER_TAX_CODE_REQUIRED";
        if (!VietnameseTaxCodeValidator.IsValid(taxCode))
            return "BUYER_TAX_CODE_INVALID";
        if (string.IsNullOrWhiteSpace(legalName))
            return "BUYER_LEGAL_NAME_REQUIRED";
        if (string.IsNullOrWhiteSpace(address))
            return "BUYER_ADDRESS_REQUIRED";

        return null;
    }
}
