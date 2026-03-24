namespace BettingApp.Server.Models;

public sealed class ForgotPasswordFormModel
{
    public string UserNameOrEmail { get; set; } = string.Empty;
}
