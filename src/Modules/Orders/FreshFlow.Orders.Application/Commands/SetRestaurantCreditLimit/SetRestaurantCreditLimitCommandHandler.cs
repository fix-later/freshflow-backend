using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.SetRestaurantCreditLimit;

internal sealed class SetRestaurantCreditLimitCommandHandler(ICreditService creditService)
    : IRequestHandler<SetRestaurantCreditLimitCommand, Result<RestaurantCreditDto>>
{
    public Task<Result<RestaurantCreditDto>> Handle(
        SetRestaurantCreditLimitCommand request, CancellationToken cancellationToken) =>
        creditService.SetCreditLimitAsync(
            request.RestaurantId, request.CreditLimit, request.Note, cancellationToken);
}
