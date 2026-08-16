using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Queries.GetRoles;

internal sealed class GetRolesQueryHandler(IRoleRepository roles)
    : IRequestHandler<GetRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(GetRolesQuery request, CancellationToken ct)
    {
        var all = await roles.GetAllAsync(ct);
        IReadOnlyList<RoleDto> dtos = all
            .Select(r => new RoleDto(r.Id, r.Name, r.Description))
            .ToList();

        return Result<IReadOnlyList<RoleDto>>.Success(dtos);
    }
}
