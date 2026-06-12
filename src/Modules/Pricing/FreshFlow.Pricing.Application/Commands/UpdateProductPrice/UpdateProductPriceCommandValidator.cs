using FluentValidation;
using FreshFlow.Pricing.Application.Options;
using Microsoft.Extensions.Options;

namespace FreshFlow.Pricing.Application.Commands.UpdateProductPrice;

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

        // ── Price rules (when provided) ───────────────────────────────────────
        When(x => x.Price.HasValue, () =>
        {
            RuleFor(x => x.Price!.Value)
                .GreaterThan(0)
                .WithMessage("Price must be greater than 0.")
                .LessThanOrEqualTo(maxPrice)
                .WithMessage($"Price must not exceed {maxPrice:N0} VND.")
                .Must(p => decimal.Round(p, 2) == p)
                .WithMessage("Price must have at most 2 decimal places.")
                .OverridePropertyName(nameof(UpdateProductPriceCommand.Price));
        });

        // ── Quantity rules (when provided) ────────────────────────────────────
        When(x => x.Quantity.HasValue, () =>
        {
            RuleFor(x => x.Quantity!.Value)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Quantity must be non-negative.")
                .OverridePropertyName(nameof(UpdateProductPriceCommand.Quantity));
        });
    }
}
