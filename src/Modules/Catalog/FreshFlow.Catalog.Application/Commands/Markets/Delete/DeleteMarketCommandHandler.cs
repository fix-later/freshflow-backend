using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Markets.Delete;

internal sealed class DeleteMarketCommandHandler(IMarketRepository markets)
    : IRequestHandler<DeleteMarketCommand, Result>
{
    public async Task<Result> Handle(DeleteMarketCommand request, CancellationToken ct)
    {
        var market = await markets.FindByIdAsync(request.Id, ct);
        if (market is null)
            return Result.Failure(Error.NotFound("Market", request.Id));

        market.Delete();
        markets.Track(market);
        await markets.SaveChangesAsync(ct);

        return Result.Success();
    }
}
