using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Queries.GetDeliveryAddresses;

public sealed record GetDeliveryAddressesQuery(Guid UserId)
    : IQuery<IReadOnlyList<DeliveryAddressDto>>;
