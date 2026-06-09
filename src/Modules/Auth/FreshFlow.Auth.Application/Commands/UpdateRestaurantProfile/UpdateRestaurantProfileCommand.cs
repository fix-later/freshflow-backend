using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.UpdateRestaurantProfile;

public sealed record UpdateRestaurantProfileCommand(
    Guid UserId,
    string Name,
    string? Address,
    string? ContactPerson,
    TimeOnly? PickupStart,
    TimeOnly? PickupEnd) : ICommand<UpdateRestaurantProfileResponse>;
