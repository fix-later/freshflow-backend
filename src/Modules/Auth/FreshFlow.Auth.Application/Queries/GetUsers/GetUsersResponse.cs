namespace FreshFlow.Auth.Application.Queries.GetUsers;

public sealed record GetUsersResponse(IReadOnlyList<UserSummaryDto> Data, PaginationMeta Meta);

public sealed record UserSummaryDto(
    Guid Id,
    string Email,
    string Role,
    bool IsActive,
    bool? IsApproved,
    DateTime CreatedAt);

public sealed record PaginationMeta(int Page, int PageSize, int Total);
