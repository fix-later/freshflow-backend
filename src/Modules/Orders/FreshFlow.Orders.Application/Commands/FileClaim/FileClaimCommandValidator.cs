using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.FileClaim;

internal sealed class FileClaimCommandValidator : AbstractValidator<FileClaimCommand>
{
    public FileClaimCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0m);
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }
}
