using BettingApp.Server.Models;

namespace BettingApp.Server.Services;

public interface IAccountEmailSender
{
    Task<AccountPreviewResponse?> SendActivationAsync(string userName, string email, string token, CancellationToken cancellationToken);

    Task<AccountPreviewResponse?> SendPasswordResetAsync(string userName, string email, string token, CancellationToken cancellationToken);
}
