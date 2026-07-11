using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.ListInbound;

internal sealed class ListInboundQueryValidator : AbstractValidator<ListInboundQuery>
{
    public ListInboundQueryValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
