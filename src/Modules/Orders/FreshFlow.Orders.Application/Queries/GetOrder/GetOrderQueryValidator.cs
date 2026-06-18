using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.GetOrder;

internal sealed class GetOrderQueryValidator : AbstractValidator<GetOrderQuery>
{
    public GetOrderQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.OrderId).NotEmpty();
    }
}
