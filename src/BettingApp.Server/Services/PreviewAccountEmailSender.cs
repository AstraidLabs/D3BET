using BettingApp.Server.Configuration;
using BettingApp.Server.Models;
using Microsoft.Extensions.Options;

namespace BettingApp.Server.Services;

public sealed class PreviewAccountEmailSender(
    IOptions<AccountEmailOptions> options,
    ILogger<PreviewAccountEmailSender> logger) : IAccountEmailSender
{
    private readonly AccountEmailOptions emailOptions = options.Value;

    public Task<AccountPreviewResponse?> SendActivationAsync(
        string userName,
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var preview = new AccountPreviewResponse(
            "activation",
            email,
            token,
            BuildLink(emailOptions.ActivationBaseUrl, email, token));

        logger.LogWarning(
            "Aktivační e-mail pro uživatele {UserName} ({Email}) byl vrácen jen jako preview token, protože není nakonfigurovaný produkční EmailSender.",
            userName,
            email);

        return Task.FromResult<AccountPreviewResponse?>(preview);
    }

    public Task<AccountPreviewResponse?> SendPasswordResetAsync(
        string userName,
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var preview = new AccountPreviewResponse(
            "password-reset",
            email,
            token,
            BuildLink(emailOptions.ResetPasswordBaseUrl, email, token));

        logger.LogWarning(
            "Reset hesla pro uživatele {UserName} ({Email}) byl vrácen jen jako preview token, protože není nakonfigurovaný produkční EmailSender.",
            userName,
            email);

        return Task.FromResult<AccountPreviewResponse?>(preview);
    }

    private static string BuildLink(string baseUrl, string userNameOrEmail, string token)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{baseUrl}{separator}user={Uri.EscapeDataString(userNameOrEmail)}&token={Uri.EscapeDataString(token)}";
    }
}
