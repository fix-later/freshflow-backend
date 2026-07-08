using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.GenerateCreditStatement;

/// <summary>
/// Generates the immutable monthly credit statement for a restaurant's billing period
/// (Asia/Ho_Chi_Minh month boundaries — DEC-CRE-03). Idempotent: if the period was already
/// generated, returns the existing statement rather than creating a duplicate.
/// Endpoint: POST /api/v1/restaurants/{restaurantId}/credit/statements/generate [admin or owning restaurant].
/// </summary>
public sealed record GenerateCreditStatementCommand(
    Guid UserId,
    bool IsAdmin,
    Guid RestaurantId,
    int Year,
    int Month) : ICommand<CreditStatementDto>;
