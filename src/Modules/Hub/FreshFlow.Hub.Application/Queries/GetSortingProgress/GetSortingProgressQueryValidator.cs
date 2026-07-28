using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.GetSortingProgress;

internal sealed class GetSortingProgressQueryValidator : AbstractValidator<GetSortingProgressQuery>
{
    public GetSortingProgressQueryValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.ServiceDate).NotEmpty();
    }
}
