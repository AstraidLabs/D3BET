using BettingApp.Server.Configuration;
using BettingApp.Server.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BettingApp.Server.Services;

public sealed class MailKitAccountEmailSender(
    IOptions<AccountEmailOptions> options,
    IGatewayOAuth2TokenClient tokenClient,
    ILogger<MailKitAccountEmailSender> logger) : IAccountEmailSender
{
    private readonly AccountEmailOptions emailOptions = options.Value;

    public async Task<AccountPreviewResponse?> SendActivationAsync(
        string userName,
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        var link = BuildLink(emailOptions.ActivationBaseUrl, email, token);
        var subject = "D3Bet aktivace účtu";
        var textBody = $"Dobrý den {userName},{Environment.NewLine}{Environment.NewLine}dokončete aktivaci účtu D3Bet pomocí odkazu:{Environment.NewLine}{link}{Environment.NewLine}{Environment.NewLine}Pokud potřebujete ruční aktivaci, použijte tento token:{Environment.NewLine}{token}";
        var htmlBody = $"""
            <p>Dobrý den {System.Net.WebUtility.HtmlEncode(userName)},</p>
            <p>dokončete aktivaci účtu D3Bet pomocí tohoto odkazu:</p>
            <p><a href="{System.Net.WebUtility.HtmlEncode(link)}">{System.Net.WebUtility.HtmlEncode(link)}</a></p>
            <p>Pokud potřebujete ruční aktivaci, použijte tento token:</p>
            <pre>{System.Net.WebUtility.HtmlEncode(token)}</pre>
            """;

        await SendAsync(email, subject, textBody, htmlBody, cancellationToken);
        logger.LogInformation("Aktivační e-mail byl odeslán uživateli {Email}.", email);
        return null;
    }

    public async Task<AccountPreviewResponse?> SendPasswordResetAsync(
        string userName,
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        var link = BuildLink(emailOptions.ResetPasswordBaseUrl, email, token);
        var subject = "D3Bet obnova hesla";
        var textBody = $"Dobrý den {userName},{Environment.NewLine}{Environment.NewLine}heslo k účtu D3Bet obnovíte přes tento odkaz:{Environment.NewLine}{link}{Environment.NewLine}{Environment.NewLine}Pokud potřebujete ruční reset, použijte tento token:{Environment.NewLine}{token}";
        var htmlBody = $"""
            <p>Dobrý den {System.Net.WebUtility.HtmlEncode(userName)},</p>
            <p>heslo k účtu D3Bet obnovíte přes tento odkaz:</p>
            <p><a href="{System.Net.WebUtility.HtmlEncode(link)}">{System.Net.WebUtility.HtmlEncode(link)}</a></p>
            <p>Pokud potřebujete ruční reset, použijte tento token:</p>
            <pre>{System.Net.WebUtility.HtmlEncode(token)}</pre>
            """;

        await SendAsync(email, subject, textBody, htmlBody, cancellationToken);
        logger.LogInformation("E-mail pro obnovu hesla byl odeslán uživateli {Email}.", email);
        return null;
    }

    private async Task SendAsync(
        string recipientEmail,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(emailOptions.FromName, emailOptions.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            TextBody = textBody,
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = 30000;

        var secureSocketOptions = ResolveSecurityOptions(emailOptions.Smtp);
        await client.ConnectAsync(emailOptions.Smtp.Host, emailOptions.Smtp.Port, secureSocketOptions, cancellationToken);

        if (emailOptions.Smtp.RequireAuthentication)
        {
            var accessToken = await tokenClient.GetAccessTokenAsync(cancellationToken);
            var oauth2 = new SaslMechanismOAuth2(emailOptions.Smtp.UserName, accessToken);
            await client.AuthenticateAsync(oauth2, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(emailOptions.FromAddress))
        {
            throw new InvalidOperationException("Chybí konfigurace AccountEmail:FromAddress.");
        }

        if (string.IsNullOrWhiteSpace(emailOptions.Smtp.Host))
        {
            throw new InvalidOperationException("Chybí konfigurace AccountEmail:Smtp:Host.");
        }

        if (emailOptions.Smtp.Port <= 0)
        {
            throw new InvalidOperationException("Konfigurace AccountEmail:Smtp:Port musí být větší než 0.");
        }

        if (emailOptions.Smtp.RequireAuthentication && string.IsNullOrWhiteSpace(emailOptions.Smtp.UserName))
        {
            throw new InvalidOperationException("Pro OAuth2 SMTP autentizaci je potřeba nastavit AccountEmail:Smtp:UserName.");
        }
    }

    private static SecureSocketOptions ResolveSecurityOptions(SmtpOptions options)
    {
        if (options.UseSsl)
        {
            return SecureSocketOptions.SslOnConnect;
        }

        if (options.UseStartTls)
        {
            return SecureSocketOptions.StartTls;
        }

        return SecureSocketOptions.None;
    }

    private static string BuildLink(string baseUrl, string userNameOrEmail, string token)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{baseUrl}{separator}user={Uri.EscapeDataString(userNameOrEmail)}&token={Uri.EscapeDataString(token)}";
    }
}
