using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.ScanInbound;

internal sealed class ScanInboundCommandValidator : AbstractValidator<ScanInboundCommand>
{
    public ScanInboundCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
    }
}
