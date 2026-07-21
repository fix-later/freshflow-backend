using System.Reflection;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Behaviors;

public sealed class HubAccessBehavior<TRequest, TResponse>(
    IHubRepository hubs,
    HubAccessChecker access)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is IHubManagementRequest management)
        {
            var managementError = await access.CheckPrivilegedAsync(management.ActorUserId, ct);
            return managementError is null ? await next(ct) : Failure(managementError);
        }

        if (request is not IHubAccessRequest scoped)
            return await next(ct);

        var hub = await hubs.FindByIdAsync(scoped.HubId, ct);
        if (hub is null)
            return Failure(Error.NotFound("HUB", scoped.HubId));

        var error = await access.CheckAsync(
            hub.Id,
            hub.IsActive,
            scoped.ActorUserId,
            scoped.BypassHubAssignment,
            ct);

        return error is null ? await next(ct) : Failure(error);
    }

    private static TResponse Failure(Error error)
    {
        var method = typeof(TResponse).GetMethod(
            nameof(Result<object>.Failure),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(Error)],
            modifiers: null);

        return method?.Invoke(null, [error]) is TResponse response
            ? response
            : throw new InvalidOperationException(
                $"{typeof(TRequest).Name} must return Result<T> to use hub access enforcement.");
    }
}
