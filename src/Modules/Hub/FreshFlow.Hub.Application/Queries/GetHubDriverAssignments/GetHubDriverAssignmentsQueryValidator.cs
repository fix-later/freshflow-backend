using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.GetHubDriverAssignments;

internal sealed class GetHubDriverAssignmentsQueryValidator
    : AbstractValidator<GetHubDriverAssignmentsQuery>
{
    public GetHubDriverAssignmentsQueryValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
    }
}
