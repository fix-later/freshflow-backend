using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.CreateClaimProofUploadSignature;

internal sealed class CreateClaimProofUploadSignatureCommandHandler(
    IOrderRepository orders,
    IRestaurantReader restaurants,
    ICloudinarySignatureService signer)
    : IRequestHandler<CreateClaimProofUploadSignatureCommand, Result<UploadSignatureResponse>>
{
    private const string ProofFolder = "freshflow/order-claims";

    public async Task<Result<UploadSignatureResponse>> Handle(
        CreateClaimProofUploadSignatureCommand request,
        CancellationToken ct)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, ct);
        if (restaurant is null)
            return Result<UploadSignatureResponse>.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        var order = await orders.FindByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result<UploadSignatureResponse>.Failure(Error.NotFound("ORDER", request.OrderId));
        if (order.RestaurantId != restaurant.RestaurantId)
            return Result<UploadSignatureResponse>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        var signed = signer.Sign(new CloudinarySignatureRequest(ProofFolder));

        return Result<UploadSignatureResponse>.Success(new UploadSignatureResponse(
            signed.Signature,
            signed.Timestamp,
            signed.ApiKey,
            signed.CloudName,
            signed.Folder));
    }
}
