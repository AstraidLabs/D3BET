namespace BettingApp.Server.Models;

public sealed record ResetPasswordRequest(
    string UserNameOrEmail,
    string Token,
    string NewPassword,
    string ConfirmPassword);
