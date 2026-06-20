namespace UserService.Infrastructure.Email;

using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using UserService.Application.Users;

/// <summary>
/// SMTP adapter (MailKit) for <see cref="IUserEmailSender"/>. Opens a fresh
/// connection per message — fine for the low volume of admin-triggered
/// account emails. In local dev it delivers to Mailpit (localhost:1025),
/// where the message can be inspected at http://localhost:8025.
/// </summary>
public sealed class SmtpUserEmailSender : IUserEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpUserEmailSender(IConfiguration configuration)
    {
        _options = new SmtpOptions();
        configuration.GetSection(SmtpOptions.SectionName).Bind(_options);
    }

    public async Task SendTemporaryPasswordAsync(
        string email, string firstName, string temporaryPassword, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(firstName, email));
        message.Subject = "Tu cuenta UMBRAL — contraseña temporal";

        message.Body = new BodyBuilder
        {
            TextBody =
                $"Hola {firstName},\n\n" +
                "Se creó tu cuenta en UMBRAL. Ingresá con estos datos:\n\n" +
                $"  Usuario:              {email}\n" +
                $"  Contraseña temporal:  {temporaryPassword}\n\n" +
                "Por seguridad, el sistema te pedirá cambiar esta contraseña la primera " +
                "vez que ingreses.\n",
            HtmlBody =
                $"""
                <div style="font-family:system-ui,Segoe UI,Arial,sans-serif;font-size:15px;color:#1a1a1a">
                  <p>Hola {firstName},</p>
                  <p>Se creó tu cuenta en <strong>UMBRAL</strong>. Ingresá con estos datos:</p>
                  <table style="border-collapse:collapse;margin:12px 0">
                    <tr><td style="padding:4px 12px 4px 0;color:#555">Usuario</td>
                        <td><code>{email}</code></td></tr>
                    <tr><td style="padding:4px 12px 4px 0;color:#555">Contraseña temporal</td>
                        <td><code style="font-size:16px;font-weight:600">{temporaryPassword}</code></td></tr>
                  </table>
                  <p style="color:#555">Por seguridad, el sistema te pedirá cambiar esta contraseña
                  la primera vez que ingreses.</p>
                </div>
                """,
        }.ToMessageBody();

        using var client = new SmtpClient();
        var security = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);

        if (!string.IsNullOrEmpty(_options.User))
            await client.AuthenticateAsync(_options.User, _options.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
