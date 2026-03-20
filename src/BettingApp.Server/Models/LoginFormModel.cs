namespace BettingApp.Server.Models;

public sealed class LoginFormModel
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = "/";
}
