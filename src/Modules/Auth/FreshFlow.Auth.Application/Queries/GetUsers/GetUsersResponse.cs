namespace FreshFlow.Auth.Application.Queries.GetUsers;

public sealed record GetUsersResponse(IReadOnlyList<UserSummaryDto> Data, PaginationMeta Meta);

public sealed record UserSummaryDto(
    Guid Id,
    string Email,
    string? FullName,
    string Role,
    bool IsActive,
    bool? IsApproved,
    Guid? RestaurantId,
    string? RestaurantStatus,
    string? RestaurantName,
    string? Phone,
    DateTime CreatedAt);

public sealed record PaginationMeta(int Page, int PageSize, int Total);
