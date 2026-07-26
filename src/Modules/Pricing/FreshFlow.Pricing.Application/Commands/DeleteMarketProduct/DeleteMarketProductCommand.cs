using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.DeleteMarketProduct;

public sealed record DeleteMarketProductCommand(Guid MarketId, Guid ProductId) : ICommand;
