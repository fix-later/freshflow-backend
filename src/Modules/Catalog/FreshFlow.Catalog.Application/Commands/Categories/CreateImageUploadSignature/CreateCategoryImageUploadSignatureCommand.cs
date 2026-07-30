using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Categories.CreateImageUploadSignature;

public sealed record CreateCategoryImageUploadSignatureCommand : IRequest<Result<UploadSignatureResponse>>;
