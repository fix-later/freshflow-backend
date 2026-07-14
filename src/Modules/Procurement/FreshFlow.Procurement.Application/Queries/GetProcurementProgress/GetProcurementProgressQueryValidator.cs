using FluentValidation;
using FreshFlow.Procurement.Domain.Enums;

namespace FreshFlow.Procurement.Application.Queries.GetProcurementProgress;

internal sealed class GetProcurementProgressQueryValidator
    : AbstractValidator<GetProcurementProgressQuery>
{
    public GetProcurementProgressQueryValidator()
    {
        RuleFor(query => query.Status)
            .Must(status =>
                status is null ||
                Enum.TryParse<ProcurementBatchStatus>(status, true, out var parsed) &&
                Enum.IsDefined(parsed))
            .WithMessage("Batch status is invalid.");
    }
}
