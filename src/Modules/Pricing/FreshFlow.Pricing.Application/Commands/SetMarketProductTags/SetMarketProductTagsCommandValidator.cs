using FluentValidation;

namespace FreshFlow.Pricing.Application.Commands.SetMarketProductTags;

/// <summary>
/// Validates structural constraints of <see cref="SetMarketProductTagsCommand"/> (400).
/// Normalization (trim/lowercase/dedupe) and the same caps are re-enforced in the domain
/// (<c>MarketProduct.SetTags</c>) as defence in depth.
/// </summary>
internal sealed class SetMarketProductTagsCommandValidator : AbstractValidator<SetMarketProductTagsCommand>
{
    public SetMarketProductTagsCommandValidator()
    {
        RuleFor(x => x.MarketId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();

        RuleFor(x => x.Tags)
            .NotNull()
            .Must(tags => tags.Count <= 8)
            .WithMessage("A market product may carry at most 8 tags.");

        RuleForEach(x => x.Tags)
            .MaximumLength(30)
            .WithMessage("Each tag must be at most 30 characters.");
    }
}
