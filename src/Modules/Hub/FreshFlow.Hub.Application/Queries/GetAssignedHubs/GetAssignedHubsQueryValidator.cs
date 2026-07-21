using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.GetAssignedHubs;

internal sealed class GetAssignedHubsQueryValidator : AbstractValidator<GetAssignedHubsQuery>
{
    public GetAssignedHubsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
