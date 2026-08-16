using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.GetClaimById;

internal sealed class GetClaimByIdQueryValidator : AbstractValidator<GetClaimByIdQuery>
{
    public GetClaimByIdQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.ClaimId).NotEmpty();
    }
}
