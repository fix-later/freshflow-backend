using FreshFlow.Invoicing.Domain.Entities;

namespace FreshFlow.Invoicing.Application.Dtos;

public sealed record InvoiceDto(
    Guid Id,
    Guid OrderId,
    Guid RestaurantId,
    string Status,
    string BuyerTaxCode,
    string BuyerLegalName,
    string? BuyerAddress,
    string? BuyerEmail,
    string? Serial,
    string? Number,
    string? TaxAuthorityCode,
    string? LookupUrl,
    DateTime? IssuedAt,
    decimal SubTotal,
    decimal VatAmount,
    decimal Total,
    int RetryCount,
    string? ErrorReason,
    string? ProviderName,
    bool IsSandbox,
    DateTime CreatedAt,
    IReadOnlyList<InvoiceLineDto> Lines)
{
    public static InvoiceDto From(Invoice i) => new(
        i.Id, i.OrderId, i.RestaurantId, i.Status.ToString(),
        i.BuyerTaxCode, i.BuyerLegalName, i.BuyerAddress, i.BuyerEmail,
        i.Serial, i.Number, i.TaxAuthorityCode, i.LookupUrl,
        i.IssuedAt, i.SubTotal, i.VatAmount, i.Total,
        i.RetryCount, i.ErrorReason, i.ProviderName, i.ProviderName == "stub", i.CreatedAt,
        i.Lines.Select(InvoiceLineDto.From).ToList());
}

public sealed record InvoiceLineDto(
    string ProductName,
    string? Unit,
    decimal Quantity,
    decimal UnitPrice,
    string VatRateCode,
    decimal VatRatePercent,
    decimal LineSubtotal,
    decimal LineVatAmount,
    decimal LineTotal)
{
    public static InvoiceLineDto From(InvoiceLine l) => new(
        l.ProductName, l.Unit, l.Quantity, l.UnitPrice, l.VatRateCode, l.VatRatePercent,
        l.LineSubtotal, l.LineVatAmount, l.LineTotal);
}

public sealed record InvoiceSummaryDto(
    Guid Id,
    Guid OrderId,
    Guid RestaurantId,
    string Status,
    string? Number,
    string? TaxAuthorityCode,
    DateTime? IssuedAt,
    decimal Total,
    string? ProviderName,
    bool IsSandbox,
    DateTime CreatedAt)
{
    public static InvoiceSummaryDto From(Invoice i) => new(
        i.Id, i.OrderId, i.RestaurantId, i.Status.ToString(),
        i.Number, i.TaxAuthorityCode, i.IssuedAt, i.Total,
        i.ProviderName, i.ProviderName == "stub", i.CreatedAt);
}
