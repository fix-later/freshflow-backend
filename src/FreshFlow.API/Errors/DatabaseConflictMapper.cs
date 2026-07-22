using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.API.Errors;

internal sealed record DatabaseConflict(string Code, string Message);

internal static class DatabaseConflictMapper
{
    public static bool TryMap(Exception? exception, out DatabaseConflict conflict)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            conflict = new DatabaseConflict(
                "OPTIMISTIC_CONCURRENCY_CONFLICT",
                "The resource was changed by another request. Reload it and try again.");
            return true;
        }

        if (exception is not DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                } postgres
            })
        {
            conflict = null!;
            return false;
        }

        conflict = postgres.ConstraintName switch
        {
            "IX_product_categories_Name" => new(
                "CATEGORY_NAME_CONFLICT", "A category with this name already exists."),
            "IX_units_of_measurement_Name" => new(
                "UNIT_NAME_CONFLICT", "A unit with this name already exists."),
            "IX_users_Email" => new(
                "EMAIL_ALREADY_EXISTS", "A user with this email already exists."),
            "IX_users_Phone" => new(
                "PHONE_ALREADY_EXISTS", "A user with this phone number already exists."),
            "ux_vehicles_plate_number_active" => new(
                "PLATE_NUMBER_DUPLICATE", "A vehicle with this plate number already exists."),
            "ux_delivery_zones_code_active" => new(
                "DELIVERY_ZONE_CODE_EXISTS", "A delivery zone with this code already exists."),
            "IX_user_market_assignments_UserId_MarketId" => new(
                "MARKET_ASSIGNMENT_CONFLICT", "This market is already assigned to the user."),
            "IX_assistant_conversations_session_id" => new(
                "SESSION_CONFLICT", "The conversation session was created by another request. Try again."),
            _ => null!
        };

        return conflict is not null;
    }
}
