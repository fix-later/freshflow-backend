using MediatR;

namespace FreshFlow.SharedKernel.Application;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
