namespace BettingApp.Wpf.ViewModels;

public sealed class BettorOptionViewModel
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public override string ToString() => Name;
}
