namespace BettingApp.Server.Models;

public sealed record UpdateAccountProfileRequest(
    string UserName,
    string Email);
