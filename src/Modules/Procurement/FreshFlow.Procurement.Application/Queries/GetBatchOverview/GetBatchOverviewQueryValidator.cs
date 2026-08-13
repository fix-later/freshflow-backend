using FluentValidation;

namespace FreshFlow.Procurement.Application.Queries.GetBatchOverview;

internal sealed class GetBatchOverviewQueryValidator : AbstractValidator<GetBatchOverviewQuery>
{
    public GetBatchOverviewQueryValidator()
    {
        RuleFor(query => query.BatchId).NotEmpty();
    }
}
