namespace FreshFlow.Invoicing.Infrastructure.CrossModule;

// Keyless projection: one row per billable order line, joining orders + order_items with the
// product's VAT rate. Mapped via ToSqlQuery (see OrderInvoiceRowConfiguration).
internal sealed class OrderInvoiceRow
{
    public Guid OrderId { get; set; }
    public Guid RestaurantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? VatRateCode { get; set; }
}
