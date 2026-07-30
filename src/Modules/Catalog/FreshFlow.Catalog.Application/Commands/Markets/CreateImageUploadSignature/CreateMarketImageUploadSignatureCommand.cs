using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Markets.CreateImageUploadSignature;

public sealed record CreateMarketImageUploadSignatureCommand : IRequest<Result<UploadSignatureResponse>>;
