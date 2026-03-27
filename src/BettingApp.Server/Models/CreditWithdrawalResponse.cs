using BettingApp.Domain.Entities;

namespace BettingApp.Server.Models;

public sealed record CreditWithdrawalResponse(
    Guid Id,
    Guid BettorId,
    decimal CreditAmount,
    decimal RealMoneyAmount,
    string RealCurrencyCode,
    decimal CreditToMoneyRateApplied,
    CreditWithdrawalRequestStatus Status,
    string Reference,
    string Reason,
    string? ProcessedReason,
    bool IsAutoProcessed,
    DateTime RequestedAtUtc,
    DateTime? ProcessedAtUtc,
    ElectronicReceiptResponse? IssuedReceipt);
