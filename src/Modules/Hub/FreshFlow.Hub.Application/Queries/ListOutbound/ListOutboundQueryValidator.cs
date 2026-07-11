using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.ListOutbound;

internal sealed class ListOutboundQueryValidator : AbstractValidator<ListOutboundQuery>
{
    public ListOutboundQueryValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
