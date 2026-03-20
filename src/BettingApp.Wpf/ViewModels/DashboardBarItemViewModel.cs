namespace BettingApp.Wpf.ViewModels;

public sealed class DashboardBarItemViewModel
{
    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Share { get; init; } = string.Empty;

    public double BarWidth { get; init; }
}
