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

    public async Task SendResetLinkAsync(string email, string rawToken, CancellationToken ct)
    {
        var opt = options.Value;
        var resetLink = $"{opt.FrontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";

        var payload = new
        {
            from = $"{opt.FromName} <{opt.FromAddress}>",
            to = new[] { email },
            subject = "Đặt lại mật khẩu FreshFlow",
            html = BuildResetEmail(resetLink)
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(ResendHttpTimeoutSeconds));

        var client = httpClientFactory.CreateClient("Resend");
        var response = await client.PostAsJsonAsync("emails", payload, cts.Token);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildResetEmail(string resetLink) => $"""
        <div style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:32px">
          <h2 style="color:#1a1a2e">Đặt lại mật khẩu</h2>
          <p>Bạn (hoặc ai đó) vừa yêu cầu đặt lại mật khẩu cho tài khoản FreshFlow.</p>
          <p>Nhấn nút bên dưới để tiếp tục. Link có hiệu lực trong <strong>15 phút</strong>.</p>
          <a href="{resetLink}"
             style="display:inline-block;margin:24px 0;padding:12px 28px;
                    background:#4f46e5;color:#fff;border-radius:6px;
                    text-decoration:none;font-weight:600">
            Đặt lại mật khẩu
          </a>
          <p style="color:#666;font-size:13px">
            Nếu bạn không yêu cầu điều này, hãy bỏ qua email này.
            Mật khẩu của bạn sẽ không thay đổi.
          </p>
          <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
          <p style="color:#999;font-size:12px">FreshFlow — Nền tảng thu mua thực phẩm chợ đầu mối</p>
        </div>
        """;
}
