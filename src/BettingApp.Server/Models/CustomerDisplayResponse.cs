namespace BettingApp.Server.Models;

public sealed record CustomerDisplayResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<CustomerDisplayMarketResponse> Markets);
