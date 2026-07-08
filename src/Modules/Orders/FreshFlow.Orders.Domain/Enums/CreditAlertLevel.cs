namespace FreshFlow.Orders.Domain.Enums;

/// <summary>
/// Credit-limit utilization threshold state tracked on <c>RestaurantCredit</c> for
/// anti-spam alerting (SCRUM-266). Ordinal order matters — comparisons like
/// <c>level &gt; LastAlertedLevel</c> rely on None &lt; Warning &lt; Exceeded.
/// </summary>
public enum CreditAlertLevel
{
    None = 0,
    Warning = 1,
    Exceeded = 2,
}
