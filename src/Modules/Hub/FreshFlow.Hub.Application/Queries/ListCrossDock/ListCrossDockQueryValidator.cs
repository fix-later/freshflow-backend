using FluentValidation;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Queries.ListCrossDock;

internal sealed class ListCrossDockQueryValidator : AbstractValidator<ListCrossDockQuery>
{
    private static readonly string[] AllowedStatuses =
    [
        CrossDockTransfer.StatusPending,
        CrossDockTransfer.StatusInProgress,
        CrossDockTransfer.StatusCompleted
    ];

    public ListCrossDockQueryValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Status)
            .Must(status => string.IsNullOrWhiteSpace(status) || AllowedStatuses.Contains(status))
            .WithMessage("Status must be pending, in_progress, or completed.");
    }
}
