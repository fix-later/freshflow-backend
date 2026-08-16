using FluentValidation;
using FreshFlow.Pricing.Application.Options;
using Microsoft.Extensions.Options;

namespace FreshFlow.Pricing.Application.Commands.CreateMarketProduct;

/// <summary>
/// Validates the structural and format constraints of <see cref="CreateMarketProductCommand"/>.
///
/// Responsibility split:
/// • 400 (this validator) — missing IDs, price exceeds max threshold,
///                          price has more than 2 decimal places.
/// • 422 (handler)        — price &lt;= 0 (INVALID_PRICE), quantity &lt; 0 (INVALID_QUANTITY).
///   These are business-rule violations handled after the structural layer.
/// </summary>
internal sealed class CreateMarketProductCommandValidator
    : AbstractValidator<CreateMarketProductCommand>
{
    public CreateMarketProductCommandValidator(IOptions<PricingOptions> options)
    {
        var maxPrice = options.Value.MaxPriceVnd;

        RuleFor(x => x.MarketId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.CreatedBy).NotEmpty();

        // NOTE: InitialPrice <= 0 is a business-rule violation → handler returns 422 INVALID_PRICE.
        RuleFor(x => x.InitialPrice)
            .LessThanOrEqualTo(maxPrice)
            .WithMessage($"Initial price must not exceed {maxPrice:N0} VND.")
            .Must(p => decimal.Round(p, 2) == p)
            .WithMessage("Initial price must have at most 2 decimal places.");

        // NOTE: InitialQuantity < 0 is a business-rule violation → handler returns 422 INVALID_QUANTITY.
    }
}
