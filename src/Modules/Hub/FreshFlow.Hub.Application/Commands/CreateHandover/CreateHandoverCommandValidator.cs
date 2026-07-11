using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.CreateHandover;

internal sealed class CreateHandoverCommandValidator : AbstractValidator<CreateHandoverCommand>
{
    public CreateHandoverCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.DeliveryRouteId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.OutboundEventId).NotEqual(Guid.Empty).When(x => x.OutboundEventId.HasValue);
        RuleFor(x => x.HandedOverBy).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
