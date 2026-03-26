namespace BettingApp.Server.Models;

public sealed record AccountSelfServiceResponse(
    string Message,
    AccountPreviewResponse? Preview = null);
