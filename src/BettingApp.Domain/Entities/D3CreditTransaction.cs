namespace BettingApp.Domain.Entities;

public sealed class D3CreditTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BettorId { get; set; }

    public D3CreditTransactionType Type { get; set; }

    public decimal CreditAmount { get; set; }

    public decimal RealMoneyAmount { get; set; }

    public string RealCurrencyCode { get; set; } = "CZK";

    public decimal MoneyToCreditRate { get; set; }

    public decimal CreditToMoneyRate { get; set; }

    public decimal MarketParticipationMultiplier { get; set; } = 1m;

    public string Reference { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Bettor? Bettor { get; set; }
}
