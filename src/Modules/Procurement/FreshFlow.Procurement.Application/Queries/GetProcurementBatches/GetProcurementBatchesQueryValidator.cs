using FluentValidation;

namespace FreshFlow.Procurement.Application.Queries.GetProcurementBatches;

internal sealed class GetProcurementBatchesQueryValidator
    : AbstractValidator<GetProcurementBatchesQuery>
{
    public GetProcurementBatchesQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
