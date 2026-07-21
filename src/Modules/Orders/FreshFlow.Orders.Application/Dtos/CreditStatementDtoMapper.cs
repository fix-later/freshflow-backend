using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Dtos;

internal static class CreditStatementDtoMapper
{
    public static CreditStatementDto ToDto(CreditStatement statement) =>
        new(
            statement.Id,
            statement.RestaurantId,
            statement.PeriodStart,
            statement.PeriodEnd,
            statement.OpeningBalance,
            statement.ClosingBalance,
            statement.TotalCharges,
            statement.TotalSettlements,
            statement.TotalRefunds,
            statement.GeneratedAt,
            CreditStatementPeriodCalculator.ResolveDueDate(statement.PeriodEnd),
            statement.Lines.Select(ToLineDto).ToList().AsReadOnly());

    public static CreditStatementSummaryDto ToSummaryDto(CreditStatement statement) =>
        new(
            statement.Id,
            statement.RestaurantId,
            statement.PeriodStart,
            statement.PeriodEnd,
            statement.OpeningBalance,
            statement.ClosingBalance,
            statement.TotalCharges,
            statement.TotalSettlements,
            statement.TotalRefunds,
            statement.GeneratedAt);

    private static CreditStatementLineDto ToLineDto(CreditStatementLine line) =>
        new(
            line.TransactionId,
            ToSnakeCase(line.Type.ToString()),
            line.Amount,
            line.BalanceAfter,
            line.OccurredAt,
            line.Note,
            line.Reference);

    private static string ToSnakeCase(string value) =>
        string.Concat(value.Select((ch, index) =>
            index > 0 && char.IsUpper(ch)
                ? "_" + char.ToLowerInvariant(ch)
                : char.ToLowerInvariant(ch).ToString()));
}
