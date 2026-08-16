using MediatR;

namespace FreshFlow.SharedKernel.Application;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

public interface ICommand : IRequest<Result>;
