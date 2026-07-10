using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.DeactivateHub;

internal sealed class DeactivateHubCommandValidator : AbstractValidator<DeactivateHubCommand>
{
    public DeactivateHubCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
    }
}
