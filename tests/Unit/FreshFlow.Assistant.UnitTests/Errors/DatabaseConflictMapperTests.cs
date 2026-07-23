using FluentAssertions;
using FreshFlow.API.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Assistant.UnitTests.Errors;

[Trait("Category", "Unit")]
public sealed class DatabaseConflictMapperTests
{
    [Theory]
    [InlineData("IX_product_categories_Name", "CATEGORY_NAME_CONFLICT")]
    [InlineData("IX_units_of_measurement_Name", "UNIT_NAME_CONFLICT")]
    [InlineData("IX_packing_codes_Code", "PACKING_CODE_CONFLICT")]
    [InlineData("IX_users_Email", "EMAIL_ALREADY_EXISTS")]
    [InlineData("IX_users_Phone", "PHONE_ALREADY_EXISTS")]
    [InlineData("ux_vehicles_plate_number_active", "PLATE_NUMBER_DUPLICATE")]
    [InlineData("ux_delivery_zones_code_active", "DELIVERY_ZONE_CODE_EXISTS")]
    [InlineData("IX_user_market_assignments_UserId_MarketId", "MARKET_ASSIGNMENT_CONFLICT")]
    [InlineData("IX_assistant_conversations_session_id", "SESSION_CONFLICT")]
    public void TryMap_KnownUniqueConstraint_ReturnsExpectedConflict(
        string constraintName,
        string expectedCode)
    {
        var postgresException = new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: PostgresErrorCodes.UniqueViolation,
            constraintName: constraintName);
        var exception = new DbUpdateException("save failed", postgresException);

        var mapped = DatabaseConflictMapper.TryMap(exception, out var conflict);

        mapped.Should().BeTrue();
        conflict.Code.Should().Be(expectedCode);
        conflict.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryMap_UnknownUniqueConstraint_ReturnsFalse()
    {
        var postgresException = new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: PostgresErrorCodes.UniqueViolation,
            constraintName: "unknown_constraint");
        var exception = new DbUpdateException("save failed", postgresException);

        var mapped = DatabaseConflictMapper.TryMap(exception, out _);

        mapped.Should().BeFalse();
    }

    [Fact]
    public void TryMap_OptimisticConcurrencyException_ReturnsConflict()
    {
        var mapped = DatabaseConflictMapper.TryMap(
            new DbUpdateConcurrencyException(),
            out var conflict);

        mapped.Should().BeTrue();
        conflict.Code.Should().Be("OPTIMISTIC_CONCURRENCY_CONFLICT");
    }
}
