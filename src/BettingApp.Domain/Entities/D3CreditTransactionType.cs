namespace BettingApp.Domain.Entities;

public enum D3CreditTransactionType
{
    TopUp = 1,
    BetPlacement = 2,
    BetPayout = 3,
    ManualAdjustment = 4,
    Refund = 5,
    WithdrawalRequest = 6,
    WithdrawalCancelled = 7,
    BetPayoutReversal = 8
}
