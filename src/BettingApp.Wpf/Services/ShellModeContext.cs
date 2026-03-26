namespace BettingApp.Wpf.Services;

public enum AppShellMode
{
    Operator = 1,
    Player = 2
}

public sealed class ShellModeContext
{
    public AppShellMode CurrentMode { get; private set; } = AppShellMode.Operator;

    public void Set(AppShellMode mode)
    {
        CurrentMode = mode;
    }
}
