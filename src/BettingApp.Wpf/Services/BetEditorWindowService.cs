using System.Windows;
using BettingApp.Wpf.ViewModels;
using BettingApp.Wpf.Views;

namespace BettingApp.Wpf.Services;

public sealed class BetEditorWindowService
{
    private BetEditorWindow? currentWindow;

    public Task ShowAsync(BetEditorViewModel viewModel)
    {
        if (currentWindow is { IsLoaded: true })
        {
            if (currentWindow.WindowState == WindowState.Minimized)
            {
                currentWindow.WindowState = WindowState.Normal;
            }

            currentWindow.Activate();
            currentWindow.Focus();
            return Task.CompletedTask;
        }

        currentWindow = new BetEditorWindow(viewModel);

        if (System.Windows.Application.Current.MainWindow is Window owner && owner != currentWindow)
        {
            currentWindow.Owner = owner;
        }

        void HandleCloseRequested(object? sender, EventArgs args)
        {
            currentWindow?.Close();
        }

        currentWindow.Closed += (_, _) =>
        {
            viewModel.CloseRequested -= HandleCloseRequested;
            viewModel.Reset();
            currentWindow = null;
        };

        viewModel.CloseRequested += HandleCloseRequested;
        currentWindow.Show();
        currentWindow.Activate();

        return Task.CompletedTask;
    }
}
