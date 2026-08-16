namespace FreshFlow.Orders.Domain.Enums;

/// <summary>
/// How a debt-payment (credit settlement) was made. Only meaningful for
/// <see cref="CreditTransactionType.Settlement"/> rows.
/// </summary>
public enum PaymentMethod
{
    BankTransfer = 1,
    Manual = 2
}
