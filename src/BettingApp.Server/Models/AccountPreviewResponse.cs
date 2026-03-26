namespace BettingApp.Server.Models;

public sealed record AccountPreviewResponse(
    string Purpose,
    string UserNameOrEmail,
    string Token,
    string? Link);
