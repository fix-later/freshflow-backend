using FluentValidation;

namespace FreshFlow.Catalog.Application.Queries.Products.GetProducts;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        // Both params are optional; defaults (1 / 20) are applied by the handler.
        // Validate only when the caller explicitly provides a value.
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.")
            .When(q => q.Page is not null);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 200)
            .WithMessage("PageSize must be between 1 and 200.")
            .When(q => q.PageSize is not null);
    }
}
