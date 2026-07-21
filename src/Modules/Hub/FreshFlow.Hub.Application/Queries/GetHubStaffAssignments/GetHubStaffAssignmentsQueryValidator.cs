using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.GetHubStaffAssignments;

internal sealed class GetHubStaffAssignmentsQueryValidator
    : AbstractValidator<GetHubStaffAssignmentsQuery>
{
    public GetHubStaffAssignmentsQueryValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
    }
}
