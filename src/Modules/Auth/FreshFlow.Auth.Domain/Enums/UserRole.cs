namespace FreshFlow.Auth.Domain.Enums;

public enum UserRole
{
    Admin,
    MarketAgent,
    HubStaff,
    Driver,
    Restaurant
}

public static class UserRoleExtensions
{
    public static string ToApiString(this UserRole role) => role switch
    {
        UserRole.Admin => "admin",
        UserRole.MarketAgent => "market_agent",
        UserRole.HubStaff => "hub_staff",
        UserRole.Driver => "driver",
        UserRole.Restaurant => "restaurant",
        _ => role.ToString().ToLowerInvariant()
    };

    public static UserRole FromApiString(string value) => value.ToLowerInvariant() switch
    {
        "admin" => UserRole.Admin,
        "market_agent" or "kiosk_staff" => UserRole.MarketAgent,
        "hub_staff" => UserRole.HubStaff,
        "driver" => UserRole.Driver,
        "restaurant" => UserRole.Restaurant,
        _ => throw new ArgumentException($"Unknown role: {value}", nameof(value))
    };
}
