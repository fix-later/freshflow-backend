using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Entities;

public sealed class OrderItem : BaseEntity
{
    private OrderItem() { } // EF Core

    public OrderItem(
        Guid marketProductId,
        string productNameSnapshot,
        int quantity,
        decimal unitPrice,
        string? packingCodeSnapshot = null)
    {
        ValidateProductNameSnapshot(productNameSnapshot);
        ValidateQuantity(quantity);
        ValidateUnitPrice(unitPrice);

        MarketProductId = marketProductId;
        ProductNameSnapshot = productNameSnapshot;
        Quantity = quantity;
        UnitPrice = unitPrice;
        PackingCodeSnapshot = packingCodeSnapshot;
    }

    public Guid OrderId { get; private set; }
    public Guid MarketProductId { get; private set; }
    public string ProductNameSnapshot { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string? PackingCodeSnapshot { get; private set; }
    public decimal? LockedUnitPrice { get; private set; }
    public decimal? LockedTotal { get; private set; }
    public string? VatRateCode { get; private set; }
    public decimal? VatRatePercent { get; private set; }
    public decimal? LockedVatAmount { get; private set; }
    public decimal? ActualQuantity { get; private set; }
    public decimal? ActualUnitPrice { get; private set; }

    public decimal Subtotal => Quantity * UnitPrice;

    /// <summary>
    /// Updates the requested quantity while the parent order is still a draft.
    /// </summary>
    public void UpdateQuantity(int quantity)
    {
        ValidateQuantity(quantity);
        Quantity = quantity;
    }

    /// <summary>
    /// Locks the unit price at order-confirm time, immune to subsequent market price changes.
    /// </summary>
    public void LockPrice(decimal lockedUnitPrice)
        => LockPricing(lockedUnitPrice, "KCT", 0m);

    public void LockPricing(decimal lockedUnitPrice, string vatRateCode, decimal vatRatePercent)
    {
        if (lockedUnitPrice < 0)
            throw new ArgumentOutOfRangeException(
                nameof(lockedUnitPrice), lockedUnitPrice, "Locked unit price must be non-negative.");

        if (string.IsNullOrWhiteSpace(vatRateCode))
            throw new ArgumentException("VAT rate code is required.", nameof(vatRateCode));
        if (vatRatePercent < 0m)
            throw new ArgumentOutOfRangeException(
                nameof(vatRatePercent), vatRatePercent, "VAT rate must be non-negative.");

        LockedUnitPrice = lockedUnitPrice;
        LockedTotal = Quantity * lockedUnitPrice;
        VatRateCode = vatRateCode;
        VatRatePercent = vatRatePercent;
        LockedVatAmount = decimal.Round(
            LockedTotal.Value * vatRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Records the quantity actually delivered (may be less than ordered if Hub Staff flags a shortage).
    /// </summary>
    public void RecordActualQuantity(decimal actualQuantity)
    {
        if (actualQuantity < 0)
            throw new ArgumentOutOfRangeException(
                nameof(actualQuantity), actualQuantity, "Actual quantity must be non-negative.");

        ActualQuantity = actualQuantity;
    }

    internal void RecordProcurementActuals(decimal actualQuantity, decimal? actualUnitPrice)
    {
        ActualQuantity = actualQuantity;
        ActualUnitPrice = actualUnitPrice;
    }

    private static void ValidateProductNameSnapshot(string productNameSnapshot)
    {
        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new ArgumentException(
                "Product name snapshot must not be empty.", nameof(productNameSnapshot));
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be greater than zero.");
    }

    private static void ValidateUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), unitPrice, "Unit price must be non-negative.");
    }
}
