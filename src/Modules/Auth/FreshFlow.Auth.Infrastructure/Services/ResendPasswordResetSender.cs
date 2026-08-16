using System.Net.Http.Json;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace FreshFlow.Auth.Infrastructure.Services;

internal sealed class ResendPasswordResetSender(
    IHttpClientFactory httpClientFactory,
    IOptions<EmailOptions> options) : IPasswordResetSender
{
    private const int ResendHttpTimeoutSeconds = 10;

    public async Task SendResetCodeAsync(string email, string code, CancellationToken ct)
    {
        var opt = options.Value;

        var payload = new
        {
            from = $"{opt.FromName} <{opt.FromAddress}>",
            to = new[] { email },
            subject = "Đặt lại mật khẩu FreshFlow",
            html = BuildResetEmail(code)
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(ResendHttpTimeoutSeconds));

        var client = httpClientFactory.CreateClient("Resend");
        var response = await client.PostAsJsonAsync("emails", payload, cts.Token);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildResetEmail(string code) => $"""
        <div style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:32px">
          <h2 style="color:#1a1a2e">Đặt lại mật khẩu</h2>
          <p>Bạn (hoặc ai đó) vừa yêu cầu đặt lại mật khẩu cho tài khoản FreshFlow.</p>
          <p>Mã đặt lại mật khẩu của bạn là:</p>
          <div style="margin:24px 0;padding:20px;background:#f4f4f8;
                      border-radius:8px;text-align:center">
            <span style="font-size:36px;font-weight:700;letter-spacing:12px;color:#4f46e5">
              {code}
            </span>
          </div>
          <p>Mã có hiệu lực trong <strong>15 phút</strong>. Không chia sẻ mã này với ai.</p>
          <p style="color:#666;font-size:13px">
            Nếu bạn không yêu cầu điều này, hãy bỏ qua email này.
            Mật khẩu của bạn sẽ không thay đổi.
          </p>
          <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
          <p style="color:#999;font-size:12px">FreshFlow — Nền tảng thu mua thực phẩm chợ đầu mối</p>
        </div>
        """;
}
