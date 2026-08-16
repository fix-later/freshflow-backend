using FluentAssertions;
using FreshFlow.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Orders.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class CreditRepositorySaveChangesTests
{
    [Fact]
    public void IsUniqueViolation_PostgresUniqueViolationSqlState_ReturnsTrue()
    {
        var pgException = new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: PostgresErrorCodes.UniqueViolation);
        var dbUpdateException = new DbUpdateException("insert failed", pgException);

        var result = CreditRepository.IsUniqueViolation(dbUpdateException);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsUniqueViolation_OtherSqlState_ReturnsFalse()
    {
        var pgException = new PostgresException(
            messageText: "foreign key violation",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: PostgresErrorCodes.ForeignKeyViolation);
        var dbUpdateException = new DbUpdateException("insert failed", pgException);

        var result = CreditRepository.IsUniqueViolation(dbUpdateException);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsUniqueViolation_NonPostgresInnerException_ReturnsFalse()
    {
        var dbUpdateException = new DbUpdateException("insert failed", new InvalidOperationException());

        var result = CreditRepository.IsUniqueViolation(dbUpdateException);

        result.Should().BeFalse();
    }

}
