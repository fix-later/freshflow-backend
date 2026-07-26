namespace FreshFlow.Invoicing.Infrastructure.CrossModule;

// Keyless projection onto the Auth-owned restaurants table for invoicing/tax details and
// userId → restaurantId resolution. Mapped via ToSqlQuery (see RestaurantTaxProfileRowConfiguration).
internal sealed class RestaurantTaxProfileRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? InvoiceLegalName { get; set; }
    public string? InvoiceAddress { get; set; }
    public string? InvoiceEmail { get; set; }
}
