using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.DriverCheckout;

public sealed record DriverCheckoutCommand(
    Guid HubId,
    Guid HandoverId,
    Guid DriverUserId) : ICommand<HubHandoverDto>;
