namespace BettingApp.Server.Services;

public sealed class FakeD3CreditPaymentGateway : ID3CreditPaymentGateway
{
    public Task<TestPaymentResult> AuthorizeTopUpAsync(decimal amount, string currencyCode, string? reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paymentReference = string.IsNullOrWhiteSpace(reference)
            ? $"FAKE-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..33]
            : reference.Trim();

        return Task.FromResult(new TestPaymentResult(
            "FakeTopUpGateway",
            paymentReference,
            Approved: amount > 0));
    }
}
