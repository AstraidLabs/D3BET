namespace BettingApp.Wpf.ViewModels;

public sealed class CustomerDisplayTileViewModel
{
    public string EventName { get; init; } = string.Empty;

    public string OddsDisplay { get; init; } = string.Empty;

    public string TotalStakeDisplay { get; init; } = string.Empty;

    public string TicketCountDisplay { get; init; } = string.Empty;

    public string AccentBrush { get; init; } = "#F97316";
}
