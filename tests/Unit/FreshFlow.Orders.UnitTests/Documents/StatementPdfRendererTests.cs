using FluentAssertions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Infrastructure.Documents;
using QuestPDF.Infrastructure;

namespace FreshFlow.Orders.UnitTests.Documents;

/// <summary>
/// Unit tests for <see cref="StatementPdfRenderer"/> — verifies it produces a well-formed PDF
/// byte stream for a statement with line items, and does not throw for an empty statement.
/// Bypasses DI, so the QuestPDF Community license is set here rather than via AddOrdersModule.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StatementPdfRendererTests
{
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();

    static StatementPdfRendererTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static CreditStatementDto BuildStatement(IReadOnlyList<CreditStatementLineDto> lines) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            1_000_000m,
            1_500_000m,
            2_000_000m,
            1_500_000m,
            0m,
            new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc),
            lines);

    [Fact]
    public void Render_StatementWithLines_ReturnsPdfBytes()
    {
        // Arrange
        var lines = new List<CreditStatementLineDto>
        {
            new(
                Guid.NewGuid(),
                "charge",
                500_000m,
                1_500_000m,
                new DateTime(2026, 6, 5, 3, 0, 0, DateTimeKind.Utc),
                "Order #1",
                "ORD-1",
                Guid.NewGuid(),
                null),
            new(
                Guid.NewGuid(),
                "settlement",
                500_000m,
                1_000_000m,
                new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc),
                "Payment received",
                "TXN-1",
                null,
                "bank_transfer"),
        };
        var statement = BuildStatement(lines);
        var sut = new StatementPdfRenderer();

        // Act
        var bytes = sut.Render(statement);

        // Assert
        bytes.Should().NotBeEmpty();
        bytes.Take(PdfMagic.Length).Should().Equal(PdfMagic);
    }

    [Fact]
    public void Render_StatementWithNoLines_DoesNotThrow()
    {
        // Arrange
        var statement = BuildStatement(Array.Empty<CreditStatementLineDto>());
        var sut = new StatementPdfRenderer();

        // Act
        var act = () => sut.Render(statement);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Render_LongDetailsAcrossMultiplePages_DoesNotThrow()
    {
        var lines = Enumerable.Range(1, 100)
            .Select(index => new CreditStatementLineDto(
                Guid.NewGuid(),
                index % 2 == 0 ? "settlement" : "charge",
                100_000m,
                1_000_000m,
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(index),
                new string('x', 120),
                $"TXN-{index}",
                index % 2 == 0 ? null : Guid.NewGuid(),
                index % 2 == 0 ? "bank_transfer" : null))
            .ToList();

        var act = () => new StatementPdfRenderer().Render(BuildStatement(lines));

        act.Should().NotThrow();
    }
}
