namespace FreshFlow.Orders.Domain.Entities;

public sealed class RestaurantCredit
{
    private RestaurantCredit() { } // EF Core

    public RestaurantCredit(Guid restaurantId, decimal creditLimit = 0m)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("Restaurant id is required.", nameof(restaurantId));

        if (creditLimit < 0m)
            throw new ArgumentOutOfRangeException(nameof(creditLimit), creditLimit, "Credit limit must be non-negative.");

        RestaurantId = restaurantId;
        CreditLimit = creditLimit;
        OutstandingBalance = 0m;
        UpdatedAt = DateTime.UtcNow;
    }

    public Guid RestaurantId { get; private set; }
    public decimal CreditLimit { get; private set; }
    public decimal OutstandingBalance { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public decimal AvailableCredit => CreditLimit - OutstandingBalance;

    public bool CanCharge(decimal amount) =>
        amount > 0m && OutstandingBalance + amount <= CreditLimit;

    public void Charge(decimal amount)
    {
        EnsurePositive(amount);

        if (OutstandingBalance + amount > CreditLimit)
            throw new InvalidOperationException("Charge would exceed the restaurant credit limit.");

        OutstandingBalance += amount;
        Touch();
    }

    public void Settle(decimal amount)
    {
        EnsurePositive(amount);

        if (amount > OutstandingBalance)
            throw new InvalidOperationException("Settlement amount cannot exceed outstanding balance.");

        OutstandingBalance -= amount;
        Touch();
    }

    public void Refund(decimal amount)
    {
        EnsurePositive(amount);

        if (amount > OutstandingBalance)
            throw new InvalidOperationException("Refund amount cannot exceed outstanding balance.");

        OutstandingBalance -= amount;
        Touch();
    }

    public void SetCreditLimit(decimal newLimit)
    {
        if (newLimit < 0m)
            throw new ArgumentOutOfRangeException(nameof(newLimit), newLimit, "Credit limit must be non-negative.");

        if (newLimit < OutstandingBalance)
            throw new InvalidOperationException("Credit limit cannot be set below the outstanding balance.");

        CreditLimit = newLimit;
        Touch();
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
