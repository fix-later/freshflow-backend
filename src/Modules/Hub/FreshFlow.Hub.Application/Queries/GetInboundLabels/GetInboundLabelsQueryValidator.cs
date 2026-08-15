using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.GetInboundLabels;

internal sealed class GetInboundLabelsQueryValidator : AbstractValidator<GetInboundLabelsQuery>
{
    public GetInboundLabelsQueryValidator()
    {
        RuleFor(query => query.HubId).NotEmpty();
        RuleFor(query => query.InboundId).NotEmpty();
    }
}
