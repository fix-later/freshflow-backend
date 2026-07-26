using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Application.Dtos;
using FreshFlow.Invoicing.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Invoicing.Application.Queries.GetInvoices;

public sealed record GetInvoicesQuery(
    Guid UserId,
    bool IsAdmin,
    Guid? RestaurantId,
    string? Status,
    int Page,
    int PageSize) : IQuery<GetInvoicesResult>;

public sealed record GetInvoicesResult(
    IReadOnlyList<InvoiceSummaryDto> Items,
    int Total,
    int Page,
    int PageSize);

internal sealed class GetInvoicesQueryHandler(
    IInvoiceRepository invoices,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GetInvoicesQuery, Result<GetInvoicesResult>>
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<Result<GetInvoicesResult>> Handle(GetInvoicesQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : request.PageSize;

        var restaurantFilter = request.RestaurantId;
        if (!request.IsAdmin)
        {
            var owned = await restaurantReader.FindRestaurantIdByUserIdAsync(request.UserId, ct);
            if (owned is null)
                return Result<GetInvoicesResult>.Failure(
                    Error.Unauthorized("FORBIDDEN", "No restaurant is associated with this account."));

            if (restaurantFilter is not null && restaurantFilter != owned)
                return Result<GetInvoicesResult>.Failure(
                    Error.NotFound("RESTAURANT", restaurantFilter.Value));

            restaurantFilter = owned;
        }

        InvoiceStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<InvoiceStatus>(request.Status, ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(typeof(InvoiceStatus), parsed))
                return Result<GetInvoicesResult>.Failure(
                    Error.Validation("VALIDATION_ERROR", $"Unknown invoice status '{request.Status}'."));
            statusFilter = parsed;
        }

        var skip = (long)(page - 1) * pageSize;
        var clampedSkip = (int)Math.Clamp(skip, 0L, int.MaxValue);
        var (items, total) = await invoices.ListAsync(
            restaurantFilter, statusFilter, clampedSkip, pageSize, ct);

        return Result<GetInvoicesResult>.Success(new GetInvoicesResult(
            items.Select(InvoiceSummaryDto.From).ToList(), total, page, pageSize));
    }
}
