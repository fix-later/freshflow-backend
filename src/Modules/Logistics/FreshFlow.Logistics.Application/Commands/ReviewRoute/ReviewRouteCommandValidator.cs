using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.ReviewRoute;

internal sealed class ReviewRouteCommandValidator : AbstractValidator<ReviewRouteCommand>
{
    public ReviewRouteCommandValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();

        RuleForEach(x => x.StopOrder)
            .NotEmpty()
            .When(x => x.StopOrder is not null);
    }
}
