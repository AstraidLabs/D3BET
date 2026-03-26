namespace BettingApp.Domain.Entities;

public sealed class BettorWallet
{
    public Guid BettorId { get; set; }

    public decimal Balance { get; set; }

    public string CreditCode { get; set; } = "D3Kredit";

    public decimal LastMoneyToCreditRate { get; set; }

    public decimal LastCreditToMoneyRate { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Bettor? Bettor { get; set; }
}
