namespace FreshFlow.Orders.Domain.Enums;

public enum OrderStatus
{
    Draft,
    Confirmed,
    Batched,
    PickedUp,
    AtHub,
    Delivering,
    Delivered,
    Cancelled
}
