using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PeriphShop.Application.Abstractions;

namespace PeriphShop.Infrastructure.Notifications;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "mailpit";
    public int Port { get; set; } = 1025;
    public bool UseSsl { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "shop@periphshop.local";
    public string FromName { get; set; } = "PeriphShop";
}

/// <summary>Отправка уведомлений по SMTP (FR-41). В стенде принимающая сторона — Mailpit.</summary>
public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            logger.LogDebug("Почтовые уведомления отключены, письмо для {To} не отправлено", to);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port,
            _options.UseSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.None,
            ct);

        if (!string.IsNullOrWhiteSpace(_options.User))
            await client.AuthenticateAsync(_options.User, _options.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Отправлено письмо «{Subject}» на {To}", subject, to);
    }
}

/// <summary>Заглушка для окружений без SMTP (например, юнит-тестов).</summary>
public class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogDebug("Письмо не отправлено (NullEmailSender): {To} / {Subject}", to, subject);
        return Task.CompletedTask;
    }
}
