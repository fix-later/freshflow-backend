using FluentValidation;

namespace FreshFlow.Procurement.Application.Commands.GenerateManifest;

internal sealed class GenerateManifestCommandValidator : AbstractValidator<GenerateManifestCommand>
{
    public GenerateManifestCommandValidator()
    {
        RuleFor(command => command.BatchId).NotEmpty();
    }
}
