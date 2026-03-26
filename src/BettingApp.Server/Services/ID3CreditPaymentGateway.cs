namespace BettingApp.Server.Services;

public interface ID3CreditPaymentGateway
{
    Task<TestPaymentResult> AuthorizeTopUpAsync(decimal amount, string currencyCode, string? reference, CancellationToken cancellationToken);
}

public sealed record TestPaymentResult(
    string GatewayName,
    string PaymentReference,
    bool Approved);
