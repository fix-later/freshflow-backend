namespace FreshFlow.Auth.Application.Abstractions;

public interface IVerificationSender
{
    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct);
}
