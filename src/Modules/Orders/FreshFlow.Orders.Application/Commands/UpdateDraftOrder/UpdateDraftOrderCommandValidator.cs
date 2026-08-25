using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.UpdateDraftOrder;

internal sealed class UpdateDraftOrderCommandValidator : AbstractValidator<UpdateDraftOrderCommand>
{
    public UpdateDraftOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
