using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;

public sealed record PreviewOrderConfirmationQuery(Guid UserId, Guid OrderId)
    : IQuery<OrderConfirmationPreviewDto>;
