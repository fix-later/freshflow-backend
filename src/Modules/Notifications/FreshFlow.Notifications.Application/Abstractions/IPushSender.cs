using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Notifications.Application.Abstractions;

public interface IPushSender
{
    public Task<Result> SendAsync(Notification notification, CancellationToken ct);
}
