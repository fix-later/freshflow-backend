using FluentValidation;
using FreshFlow.Pricing.Application.Options;
using Microsoft.Extensions.Options;

namespace FreshFlow.Pricing.Application.Commands.UpdateProductPrice;

/// <summary>
/// Validates the structural and format constraints of <see cref="UpdateProductPriceCommand"/>.
///
/// Responsibility split:
/// • 400 (this validator) — missing IDs, at-least-one-field, price exceeds max threshold,
///                          price has more than 2 decimal places.
/// • 422 (handler)        — price ≤ 0 (INVALID_PRICE), quantity &lt; 0 (INVALID_QUANTITY).
///   These are business-rule violations handled after the structural layer.
/// </summary>
internal sealed class UpdateProductPriceCommandValidator
    : AbstractValidator<UpdateProductPriceCommand>
{
    public UpdateProductPriceCommandValidator(IOptions<PricingOptions> options)
    {
        var maxPrice = options.Value.MaxPriceVnd;

        // ── Path-param identity guards ────────────────────────────────────────
        RuleFor(x => x.MarketId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.AgentUserId).NotEmpty();

        // ── At-least-one-field guard ──────────────────────────────────────────
        RuleFor(x => x)
            .Must(cmd => cmd.Price.HasValue || cmd.Quantity.HasValue)
            .WithErrorCode("AtLeastOneFieldRequired")
            .WithMessage("At least one of 'price' or 'quantity' must be provided.");

        // ── Price structural rules (when provided) ────────────────────────────
        // NOTE: price ≤ 0 is a business-rule violation → handled by the handler (422 INVALID_PRICE).
        When(x => x.Price.HasValue, () =>
        {
            RuleFor(x => x.Price!.Value)
                .LessThanOrEqualTo(maxPrice)
                .WithMessage($"Price must not exceed {maxPrice:N0} VND.")
                .Must(p => decimal.Round(p, 2) == p)
                .WithMessage("Price must have at most 2 decimal places.")
                .OverridePropertyName(nameof(UpdateProductPriceCommand.Price));
        });

        // ── Quantity structural rules ─────────────────────────────────────────
        // NOTE: quantity < 0 is a business-rule violation → handled by the handler (422 INVALID_QUANTITY).
        // The `int?` binding already rejects non-integer values at the model-binding layer (400).
    }
}
