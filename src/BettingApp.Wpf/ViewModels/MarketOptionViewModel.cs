namespace BettingApp.Wpf.ViewModels;

public sealed class MarketOptionViewModel
{
    public Guid Id { get; init; }

    public string EventName { get; init; } = string.Empty;

    public decimal CurrentOdds { get; init; }

    public bool IsActive { get; init; }

    public string CurrentOddsDisplay => CurrentOdds.ToString("0.00");

    public override string ToString() => $"{EventName} | kurz {CurrentOddsDisplay}";
}
