using BettingApp.Server.Configuration;
using BettingApp.Server.Models;
using Microsoft.Extensions.Options;

namespace BettingApp.Server.Services;

public sealed class AccountEmailSenderFactory(
    IOptions<AccountEmailOptions> options,
    PreviewAccountEmailSender previewSender,
    MailKitAccountEmailSender mailKitSender) : IAccountEmailSender
{
    private readonly AccountEmailOptions emailOptions = options.Value;

    public Task<AccountPreviewResponse?> SendActivationAsync(string userName, string email, string token, CancellationToken cancellationToken)
    {
        return ResolveSender().SendActivationAsync(userName, email, token, cancellationToken);
    }

    public Task<AccountPreviewResponse?> SendPasswordResetAsync(string userName, string email, string token, CancellationToken cancellationToken)
    {
        return ResolveSender().SendPasswordResetAsync(userName, email, token, cancellationToken);
    }

    private IAccountEmailSender ResolveSender()
    {
        return emailOptions.Mode.Trim().ToUpperInvariant() switch
        {
            "GATEWAYOAUTH2" => mailKitSender,
            "SMTP4DEV" => mailKitSender,
            _ => previewSender
        };
    }
}
