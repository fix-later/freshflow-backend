namespace FreshFlow.Invoicing.Application.Common;

/// <summary>
/// Names of the non-goods lines the issuance service appends to an invoice. The PDF matches on
/// these to group charges apart from the goods, so both sides must use the same constant.
/// </summary>
public static class InvoiceLineNames
{
    public const string DeliveryFee = "Phí giao hàng";
    public const string DeliveryFeeUnit = "lần";
}
