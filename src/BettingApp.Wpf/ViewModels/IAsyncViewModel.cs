namespace BettingApp.Wpf.ViewModels;

public interface IAsyncViewModel
{
    Task InitializeAsync();

    Task ShutdownAsync();
}
