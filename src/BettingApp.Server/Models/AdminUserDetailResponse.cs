using BettingApp.Domain.Entities;

namespace BettingApp.Server.Models;

public sealed record AdminUserDetailResponse(
    string Id,
    string UserName,
    string? Email,
    bool EmailConfirmed,
    bool IsBlocked,
    string[] Roles,
    string[] AvailableRoles,
    Guid? BettorId,
    AdminUserWalletResponse Wallet,
    AdminUserBetResponse[] Bets,
    AdminUserCreditTransactionResponse[] Transactions,
    CreditWithdrawalResponse[] Withdrawals,
    ElectronicReceiptResponse[] Receipts);

public sealed record AdminUserWalletResponse(
    Guid? BettorId,
    string DisplayName,
    decimal Balance,
    string CreditCode,
    decimal MoneyToCreditRate,
    decimal CreditToMoneyRate);

public sealed record AdminUserBetResponse(
    Guid Id,
    Guid? BettingMarketId,
    string EventName,
    decimal Odds,
    decimal Stake,
    string StakeCurrencyCode,
    decimal StakeRealMoneyEquivalent,
    decimal PotentialPayout,
    BetOutcomeStatus OutcomeStatus,
    bool IsPayoutProcessed,
    DateTime PlacedAtUtc);

public sealed record AdminUserCreditTransactionResponse(
    Guid Id,
    D3CreditTransactionType Type,
    decimal CreditAmount,
    decimal RealMoneyAmount,
    string RealCurrencyCode,
    string Description,
    string Reference,
    DateTime CreatedAtUtc);
