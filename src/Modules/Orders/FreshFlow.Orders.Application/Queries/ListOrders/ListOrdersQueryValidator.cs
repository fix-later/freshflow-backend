using FluentValidation;
using FreshFlow.Orders.Application.Queries;

namespace FreshFlow.Orders.Application.Queries.ListOrders;

internal sealed class ListOrdersQueryValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.Page).GreaterThan(0);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.RestaurantId).NotEqual(Guid.Empty).When(q => q.RestaurantId.HasValue);
        RuleFor(q => q.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || OrderQueryParsing.TryParseStatus(s, out _))
            .WithMessage("Status is invalid.");
        RuleFor(q => q.Sort)
            .Must(s => OrderQueryParsing.TryParseSort(s, out _))
            .WithMessage("Sort must be 'createdAt:asc' or 'createdAt:desc'.");
        RuleFor(q => q)
            .Must(q =>
                !q.From.HasValue
                || !q.To.HasValue
                || q.From.Value <= OrderQueryParsing.NormalizeCreatedTo(q.To)!.Value)
            .WithMessage("'From' must be before or equal to 'To'.");
    }
}
