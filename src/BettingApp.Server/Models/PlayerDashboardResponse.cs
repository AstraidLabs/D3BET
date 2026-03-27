namespace BettingApp.Server.Models;

public sealed record PlayerDashboardResponse(
    AccountProfileResponse Profile,
    D3CreditWalletResponse Wallet,
    IReadOnlyList<PlayerMarketSummaryResponse> Markets,
    IReadOnlyList<PlayerBetSummaryResponse> RecentBets,
    IReadOnlyList<CreditWithdrawalResponse> RecentWithdrawals,
    IReadOnlyList<ElectronicReceiptResponse> RecentReceipts);
