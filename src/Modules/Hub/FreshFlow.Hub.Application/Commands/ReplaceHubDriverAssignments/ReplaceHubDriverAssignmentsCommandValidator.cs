using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.ReplaceHubDriverAssignments;

internal sealed class ReplaceHubDriverAssignmentsCommandValidator
    : AbstractValidator<ReplaceHubDriverAssignmentsCommand>
{
    public ReplaceHubDriverAssignmentsCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.DriverUserIds).NotNull();

        RuleForEach(x => x.DriverUserIds)
            .NotEmpty()
            .WithName("DriverUserIds")
            .When(x => x.DriverUserIds is not null);

        RuleFor(x => x.DriverUserIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Driver user IDs must be unique.")
            .When(x => x.DriverUserIds is not null && x.DriverUserIds.Count > 0);
    }
}
