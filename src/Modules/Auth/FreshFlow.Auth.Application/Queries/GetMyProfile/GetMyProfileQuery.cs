using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Queries.GetMyProfile;

public sealed record GetMyProfileQuery(Guid UserId) : IQuery<GetMyProfileResponse>;
