using FluentValidation;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Queries.ListDiscrepancies;

internal sealed class ListDiscrepanciesQueryValidator : AbstractValidator<ListDiscrepanciesQuery>
{
    public ListDiscrepanciesQueryValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Status)
            .Must(status => status is null
                or HubDiscrepancy.StatusOpen
                or HubDiscrepancy.StatusAcknowledged);
    }
}
