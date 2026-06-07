namespace FreshFlow.Auth.Domain.Enums;

/// <summary>
/// Well-known role name strings that match the values seeded in the <c>roles</c> table.
/// Use these constants instead of magic strings when comparing <c>user.Role.Name</c>.
/// </summary>
public static class RoleNames
{
    public const string Admin = "admin";
    public const string MarketAgent = "market_agent";
    public const string Restaurant = "restaurant";
    public const string HubStaff = "hub_staff";
    public const string Driver = "driver";
    public const string OperationsManager = "operations_manager";
}
