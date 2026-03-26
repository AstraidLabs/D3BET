namespace BettingApp.Server.Models;

public sealed record RegisterAccountRequest(
    string UserName,
    string Email,
    string Password,
    string ConfirmPassword);
