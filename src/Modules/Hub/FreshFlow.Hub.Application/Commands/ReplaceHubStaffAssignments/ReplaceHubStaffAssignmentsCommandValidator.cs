using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.ReplaceHubStaffAssignments;

internal sealed class ReplaceHubStaffAssignmentsCommandValidator
    : AbstractValidator<ReplaceHubStaffAssignmentsCommand>
{
    public ReplaceHubStaffAssignmentsCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.StaffUserIds).NotNull();

        RuleForEach(x => x.StaffUserIds)
            .NotEmpty()
            .WithName("StaffUserIds")
            .When(x => x.StaffUserIds is not null);

        RuleFor(x => x.StaffUserIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Staff user IDs must be unique.")
            .When(x => x.StaffUserIds is not null && x.StaffUserIds.Count > 0);
    }
}
