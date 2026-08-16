using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.CreateDiscrepancyProofUploadSignature;

internal sealed class CreateDiscrepancyProofUploadSignatureCommandValidator
    : AbstractValidator<CreateDiscrepancyProofUploadSignatureCommand>
{
    public CreateDiscrepancyProofUploadSignatureCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.InboundEventId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}
