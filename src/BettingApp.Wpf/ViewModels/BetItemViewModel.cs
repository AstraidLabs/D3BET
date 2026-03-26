using BettingApp.Wpf.Commands;
using BettingApp.Domain.Entities;

namespace BettingApp.Wpf.ViewModels;

public sealed class BetItemViewModel
{
    public Guid Id { get; init; }

    public Guid BettorId { get; init; }

    public Guid? BettingMarketId { get; init; }

    public string EventName { get; init; } = string.Empty;

    public string BettorName { get; init; } = string.Empty;

    public decimal Odds { get; init; }

    public decimal Stake { get; init; }

    public string StakeCurrencyCode { get; init; } = string.Empty;

    public decimal StakeRealMoneyEquivalent { get; init; }

    public bool IsWinning { get; init; }

    public BetOutcomeStatus OutcomeStatus { get; init; }

    public bool IsCommissionFeePaid { get; init; }

    public string OddsDisplay { get; init; } = string.Empty;

    public string StakeDisplay { get; init; } = string.Empty;

    public string PotentialPayoutDisplay { get; init; } = string.Empty;

    public string PlacedAtDisplay { get; init; } = string.Empty;

    public DateTime PlacedAtLocal { get; init; }

    public string OutcomeBadgeText => OutcomeStatus switch
    {
        BetOutcomeStatus.Won => "Výherní",
        BetOutcomeStatus.Lost => "Nevýherní",
        _ => "Čeká na vyhodnocení"
    };

    public string OutcomeBadgeBackground => OutcomeStatus switch
    {
        BetOutcomeStatus.Won => "#14532D",
        BetOutcomeStatus.Lost => "#7F1D1D",
        _ => "#334155"
    };

    public string OutcomeBadgeForeground => OutcomeStatus switch
    {
        BetOutcomeStatus.Won => "#DCFCE7",
        BetOutcomeStatus.Lost => "#FECACA",
        _ => "#E2E8F0"
    };

    public AsyncRelayCommand<BetItemViewModel>? EditCommand { get; init; }

    public AsyncRelayCommand<BetItemViewModel>? AddBettorCommand { get; init; }

    public AsyncRelayCommand<BetItemViewModel>? MarkWonCommand { get; init; }

    public AsyncRelayCommand<BetItemViewModel>? MarkLostCommand { get; init; }

    public AsyncRelayCommand<BetItemViewModel>? ResetOutcomeCommand { get; init; }

    public AsyncRelayCommand<BetItemViewModel>? DeleteCommand { get; init; }
}
