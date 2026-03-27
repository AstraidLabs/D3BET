namespace BettingApp.Domain.Entities;

public sealed class ElectronicReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BettorId { get; set; }

    public ElectronicReceiptType Type { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public decimal CreditAmount { get; set; }

    public decimal RealMoneyAmount { get; set; }

    public string RealCurrencyCode { get; set; } = "CZK";

    public decimal MoneyToCreditRate { get; set; }

    public decimal CreditToMoneyRate { get; set; }

    public string Reference { get; set; } = string.Empty;

    public Guid? RelatedTransactionId { get; set; }

    public Guid? RelatedBetId { get; set; }

    public Guid? RelatedWithdrawalRequestId { get; set; }

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    public Bettor? Bettor { get; set; }
}
