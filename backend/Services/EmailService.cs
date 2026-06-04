using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace backend.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<(bool success, string? code)> SendVerificationCodeAsync(string toEmail, string code)
    {
        var smtp = _config.GetSection("Smtp");
        var host = smtp["Host"];
        var username = smtp["Username"];
        var password = smtp["Password"];
        var from = smtp["From"];
        var port = int.TryParse(smtp["Port"], out var p) ? p : 587;

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("SMTP 未配置，验证码: {Code}", code);
            return (true, code);
        }

        try
        {
            using var client = new SmtpClient();

            // 根据端口自动选择安全方式
            var secureOption = port == 465 ? SecureSocketOptions.SslOnConnect
                : port == 587 ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            _logger.LogInformation("正在连接 SMTP: {Host}:{Port}, Secure={Option}", host, port, secureOption);

            await client.ConnectAsync(host, port, secureOption);
            await client.AuthenticateAsync(username, password);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("权限管理系统", from ?? username));
            message.To.Add(new MailboxAddress(toEmail, toEmail));
            message.Subject = "密码重置验证码";

            message.Body = new TextPart("html")
            {
                Text = $@"<div style='font-family:Arial,sans-serif;padding:20px;'>
                    <h2>密码重置验证码</h2>
                    <p>您的验证码是：</p>
                    <h1 style='color:#409EFF;letter-spacing:8px;'>{code}</h1>
                    <p>验证码 5 分钟内有效，请勿泄漏给他人。</p>
                    <hr/>
                    <p style='color:#999;font-size:12px;'>如果这不是您本人的操作，请忽略此邮件。</p>
                </div>"
            };

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("验证码已发送至 {Email}", toEmail);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送验证码邮件失败");
            return (false, null);
        }
    }
}
