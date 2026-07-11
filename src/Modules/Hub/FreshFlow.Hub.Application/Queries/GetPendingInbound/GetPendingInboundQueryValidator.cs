using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.GetPendingInbound;

internal sealed class GetPendingInboundQueryValidator : AbstractValidator<GetPendingInboundQuery>
{
    public GetPendingInboundQueryValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
