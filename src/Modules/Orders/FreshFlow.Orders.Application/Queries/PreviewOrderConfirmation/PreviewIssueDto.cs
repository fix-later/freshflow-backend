namespace FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;

/// <summary>
/// A single reason an order would fail to confirm, surfaced for preview display.
/// </summary>
public sealed record PreviewIssueDto(string Code, string Message);
