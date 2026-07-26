using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Invoicing.Application.Queries.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(Guid UserId, bool IsAdmin, Guid InvoiceId) : IQuery<InvoiceDto>;

internal sealed class GetInvoiceByIdQueryHandler(
    IInvoiceRepository invoices,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(GetInvoiceByIdQuery request, CancellationToken ct)
    {
        var invoice = await invoices.FindByIdAsync(request.InvoiceId, ct);
        if (invoice is null)
            return Result<InvoiceDto>.Failure(Error.NotFound("INVOICE", request.InvoiceId));

        if (!request.IsAdmin)
        {
            var owned = await restaurantReader.FindRestaurantIdByUserIdAsync(request.UserId, ct);
            // IDOR guard: an invoice belonging to another restaurant looks "not found" to a non-admin.
            if (owned is null || owned != invoice.RestaurantId)
                return Result<InvoiceDto>.Failure(Error.NotFound("INVOICE", request.InvoiceId));
        }

        return Result<InvoiceDto>.Success(InvoiceDto.From(invoice));
    }
}
