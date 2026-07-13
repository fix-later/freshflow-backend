using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.AttachProofOfDelivery;

internal sealed class AttachProofOfDeliveryCommandValidator : AbstractValidator<AttachProofOfDeliveryCommand>
{
    public AttachProofOfDeliveryCommandValidator()
    {
        RuleFor(x => x.DeliveryId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.ProofUrl)
            .NotEmpty()
            .MaximumLength(512)
            .Must(IsCloudinaryHttpsUrl)
            .WithMessage("ProofUrl must be an absolute HTTPS Cloudinary resource URL.");
    }

    private static bool IsCloudinaryHttpsUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase);
}
