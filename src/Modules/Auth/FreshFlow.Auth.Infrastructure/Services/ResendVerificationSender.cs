using System.Net.Http.Json;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace FreshFlow.Auth.Infrastructure.Services;

internal sealed class ResendVerificationSender(
    IHttpClientFactory httpClientFactory,
    IOptions<EmailOptions> options) : IVerificationSender
{
    private const int ResendHttpTimeoutSeconds = 10;

    public async Task SendVerificationCodeAsync(string email, string code, CancellationToken ct)
    {
        var opt = options.Value;

        var payload = new
        {
            from = $"{opt.FromName} <{opt.FromAddress}>",
            to = new[] { email },
            subject = "Mã xác thực FreshFlow",
            html = BuildVerificationEmail(code)
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(ResendHttpTimeoutSeconds));

        var client = httpClientFactory.CreateClient("Resend");
        var response = await client.PostAsJsonAsync("emails", payload, cts.Token);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildVerificationEmail(string code) => $"""
        <div style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:32px">
          <h2 style="color:#1a1a2e">Xác thực email</h2>
          <p>Mã xác thực của bạn là:</p>
          <div style="margin:24px 0;padding:20px;background:#f4f4f8;
                      border-radius:8px;text-align:center">
            <span style="font-size:36px;font-weight:700;letter-spacing:12px;color:#4f46e5">
              {code}
            </span>
          </div>
          <p>Mã có hiệu lực trong <strong>10 phút</strong>. Không chia sẻ mã này với ai.</p>
          <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
          <p style="color:#999;font-size:12px">FreshFlow — Nền tảng thu mua thực phẩm chợ đầu mối</p>
        </div>
        """;
}
