namespace BettingApp.Server.Models;

public sealed record ActivateAccountRequest(
    string UserNameOrEmail,
    string Token);
