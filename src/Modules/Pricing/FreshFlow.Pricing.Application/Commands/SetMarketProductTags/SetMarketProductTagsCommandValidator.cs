using FluentValidation;

namespace FreshFlow.Pricing.Application.Commands.SetMarketProductTags;

/// <summary>
/// Validates structural constraints of <see cref="SetMarketProductTagsCommand"/> (400).
/// Existence of each <c>TagId</c> in the live catalog is a DB lookup, not a structural rule —
/// checked in the handler (also 400, VALIDATION_ERROR).
/// </summary>
internal sealed class SetMarketProductTagsCommandValidator : AbstractValidator<SetMarketProductTagsCommand>
{
    public SetMarketProductTagsCommandValidator()
    {
        RuleFor(x => x.MarketId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();

        RuleFor(x => x.TagIds)
            .NotNull()
            .Must(tagIds => tagIds.Count <= 8)
            .WithMessage("A market product may carry at most 8 tags.");

        RuleForEach(x => x.TagIds).NotEmpty();
    }
}
