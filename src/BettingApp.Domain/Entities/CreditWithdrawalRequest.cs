namespace BettingApp.Domain.Entities;

public sealed class CreditWithdrawalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BettorId { get; set; }

    public decimal CreditAmount { get; set; }

    public decimal RealMoneyAmount { get; set; }

    public string RealCurrencyCode { get; set; } = "CZK";

    public decimal CreditToMoneyRateApplied { get; set; }

    public CreditWithdrawalRequestStatus Status { get; set; } = CreditWithdrawalRequestStatus.Pending;

    public string Reference { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? ProcessedReason { get; set; }

    public bool IsAutoProcessed { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAtUtc { get; set; }

    public Bettor? Bettor { get; set; }
}
