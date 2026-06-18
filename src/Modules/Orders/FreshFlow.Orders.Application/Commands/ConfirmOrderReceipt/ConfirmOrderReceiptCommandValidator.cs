using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.ConfirmOrderReceipt;

internal sealed class ConfirmOrderReceiptCommandValidator : AbstractValidator<ConfirmOrderReceiptCommand>
{
    public ConfirmOrderReceiptCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
