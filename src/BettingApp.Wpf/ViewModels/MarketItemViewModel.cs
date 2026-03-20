using BettingApp.Wpf.Commands;

namespace BettingApp.Wpf.ViewModels;

public sealed class MarketItemViewModel
{
    public Guid Id { get; init; }

    public string EventName { get; init; } = string.Empty;

    public decimal OpeningOdds { get; init; }

    public decimal CurrentOdds { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAtLocal { get; init; }

    public string OpeningOddsDisplay { get; init; } = string.Empty;

    public string CurrentOddsDisplay { get; init; } = string.Empty;

    public string CreatedAtDisplay { get; init; } = string.Empty;

    public string AvailabilityText => IsActive ? "Aktivní" : "Uzavřená";

    public AsyncRelayCommand<MarketItemViewModel>? EditCommand { get; init; }
}
