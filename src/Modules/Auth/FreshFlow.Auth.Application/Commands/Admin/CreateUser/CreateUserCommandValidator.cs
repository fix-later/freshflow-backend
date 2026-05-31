using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.Admin.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    private static readonly HashSet<string> ValidRoles =
        ["market_agent", "kiosk_staff", "hub_staff", "driver", "restaurant"];

    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => ValidRoles.Contains(r.ToLowerInvariant()))
            .WithMessage("Role must be one of: market_agent, kiosk_staff, hub_staff, driver, restaurant");

        RuleFor(x => x.MarketId)
            .NotEmpty()
            .When(x => x.Role?.ToLowerInvariant() is "market_agent" or "kiosk_staff")
            .WithMessage("MarketId is required for market_agent and kiosk_staff roles.");

        RuleFor(x => x.RestaurantName)
            .NotEmpty().MaximumLength(200)
            .When(x => x.Role?.ToLowerInvariant() is "restaurant")
            .WithMessage("RestaurantName is required for restaurant role.");
    }
}
