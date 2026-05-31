namespace FreshFlow.Auth.Application.Abstractions;

public interface IDriverProfileCreator
{
    public Task CreateAsync(Guid userId, CancellationToken ct);
}
