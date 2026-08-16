using FluentValidation;

namespace FreshFlow.Logistics.Application.Queries.GetDriverRoutesToday;

internal sealed class GetDriverRoutesTodayQueryValidator : AbstractValidator<GetDriverRoutesTodayQuery>
{
    public GetDriverRoutesTodayQueryValidator()
    {
        RuleFor(x => x.DriverUserId).NotEmpty();
    }
}
