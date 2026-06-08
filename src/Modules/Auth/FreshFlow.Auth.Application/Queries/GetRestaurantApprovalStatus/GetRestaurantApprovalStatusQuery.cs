using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Queries.GetRestaurantApprovalStatus;

public sealed record GetRestaurantApprovalStatusQuery(Guid UserId)
    : IQuery<GetRestaurantApprovalStatusResponse>;
