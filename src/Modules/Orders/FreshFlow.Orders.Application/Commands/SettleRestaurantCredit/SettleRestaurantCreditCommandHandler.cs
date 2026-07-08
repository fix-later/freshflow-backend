using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.SettleRestaurantCredit;

internal sealed class SettleRestaurantCreditCommandHandler(ICreditService creditService)
    : IRequestHandler<SettleRestaurantCreditCommand, Result<RestaurantCreditDto>>
{
    public Task<Result<RestaurantCreditDto>> Handle(
        SettleRestaurantCreditCommand request, CancellationToken cancellationToken) =>
        creditService.SettleAsync(
            request.RestaurantId,
            request.Amount,
            request.PaymentMethod,
            request.Reference,
            request.Note,
            cancellationToken);
}
