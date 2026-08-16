using FluentValidation;

namespace FreshFlow.Pricing.Application.Commands.UpdateAvailableQuantity;

/// <summary>
/// Validates structural constraints of <see cref="UpdateAvailableQuantityCommand"/>.
///
/// Responsibility split:
/// • 400 (this validator) — missing IDs; non-integer body is rejected at model-binding layer.
/// • 422 (handler)        — quantity &lt; 0 → INVALID_QUANTITY (business rule).
/// </summary>
internal sealed class UpdateAvailableQuantityCommandValidator
    : AbstractValidator<UpdateAvailableQuantityCommand>
{
    public UpdateAvailableQuantityCommandValidator()
    {
        // ── Path-param identity guards ────────────────────────────────────────
        RuleFor(x => x.MarketId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.AgentUserId).NotEmpty();

        // NOTE: quantity < 0 is a business-rule violation → 422 INVALID_QUANTITY in handler.
        // The `int` binding already rejects non-integer values at the model-binding layer (400).
    }
}
