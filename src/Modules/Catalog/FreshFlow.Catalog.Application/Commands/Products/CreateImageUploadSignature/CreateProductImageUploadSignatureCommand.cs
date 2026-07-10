using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.CreateImageUploadSignature;

public sealed record CreateProductImageUploadSignatureCommand : IRequest<Result<UploadSignatureResponse>>;
