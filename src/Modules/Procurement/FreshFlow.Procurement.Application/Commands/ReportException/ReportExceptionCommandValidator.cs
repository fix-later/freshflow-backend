using FluentValidation;
using FreshFlow.Procurement.Domain.Enums;

namespace FreshFlow.Procurement.Application.Commands.ReportException;

internal sealed class ReportExceptionCommandValidator : AbstractValidator<ReportExceptionCommand>
{
    public ReportExceptionCommandValidator()
    {
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.AgentUserId).NotEmpty();
        RuleFor(command => command.MarketProductId).NotEmpty();
        RuleFor(command => command.Type)
            .NotEmpty()
            .Must(type =>
                Enum.TryParse<ProcurementExceptionType>(type, true, out var parsed) &&
                Enum.IsDefined(parsed))
            .WithMessage("Exception type is invalid.");
        RuleFor(command => command.ReportedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Note).MaximumLength(500);
        RuleFor(command => command.ProofImageUrl)
            .MaximumLength(500)
            .Must(BeCloudinaryUrl)
            .When(command => !string.IsNullOrWhiteSpace(command.ProofImageUrl))
            .WithMessage("Proof image URL must be an HTTPS Cloudinary URL.");
    }

    private static bool BeCloudinaryUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase);
}
