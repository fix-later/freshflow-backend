namespace FreshFlow.Auth.Application.Abstractions;

public interface IVerificationSender
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct);
}
