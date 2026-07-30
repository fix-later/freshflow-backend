using FluentValidation;

namespace FreshFlow.Procurement.Application.Commands.ResetBatchingDay;

internal sealed class ResetBatchingDayCommandValidator : AbstractValidator<ResetBatchingDayCommand>
{
    public ResetBatchingDayCommandValidator()
    {
        RuleFor(command => command.TargetDate).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.Confirmation)
            .Equal(command => $"RESET {command.TargetDate:yyyy-MM-dd}")
            .WithMessage("'Confirmation' must exactly match 'RESET yyyy-MM-dd'.");
    }
}
