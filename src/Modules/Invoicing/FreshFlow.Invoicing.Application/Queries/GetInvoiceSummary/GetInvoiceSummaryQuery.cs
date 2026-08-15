using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Invoicing.Application.Queries.GetInvoiceSummary;

public sealed record GetInvoiceSummaryQuery(
    Guid UserId,
    bool IsAdmin,
    Guid? RestaurantId,
    DateTimeOffset? From,
    DateTimeOffset? To) : IQuery<InvoiceSummaryReport>;

public sealed record InvoiceSummaryReport(
    Guid? RestaurantId,
    DateTimeOffset From,
    DateTimeOffset To,
    int InvoiceCount,
    decimal SubTotal,
    decimal VatAmount,
    decimal Total,
    string Notice);

internal sealed class GetInvoiceSummaryQueryHandler(
    IInvoiceRepository invoices,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GetInvoiceSummaryQuery, Result<InvoiceSummaryReport>>
{
    public async Task<Result<InvoiceSummaryReport>> Handle(
        GetInvoiceSummaryQuery request, CancellationToken ct)
    {
        if (request.From is null || request.To is null || request.From >= request.To)
            return Result<InvoiceSummaryReport>.Failure(Error.Validation(
                "VALIDATION_ERROR", "'from' and 'to' are required, and 'from' must be before 'to'."));

        var restaurantFilter = request.RestaurantId;
        if (!request.IsAdmin)
        {
            var owned = await restaurantReader.FindRestaurantIdByUserIdAsync(request.UserId, ct);
            if (owned is null)
                return Result<InvoiceSummaryReport>.Failure(
                    Error.Unauthorized("FORBIDDEN", "No restaurant is associated with this account."));

            if (restaurantFilter is not null && restaurantFilter != owned)
                return Result<InvoiceSummaryReport>.Failure(
                    Error.NotFound("RESTAURANT", restaurantFilter.Value));

            restaurantFilter = owned;
        }

        var totals = await invoices.SummarizeIssuedAsync(
            restaurantFilter,
            request.From.Value.UtcDateTime,
            request.To.Value.UtcDateTime,
            ct);

        return Result<InvoiceSummaryReport>.Success(new InvoiceSummaryReport(
            restaurantFilter,
            request.From.Value,
            request.To.Value,
            totals.Count,
            totals.SubTotal,
            totals.VatAmount,
            totals.Total,
            "Bảng tổng hợp hóa đơn - không phải hóa đơn VAT."));
    }
}
