using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.UpdateOrderNotes;

internal sealed class UpdateOrderNotesCommandValidator : AbstractValidator<UpdateOrderNotesCommand>
{
    public UpdateOrderNotesCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
