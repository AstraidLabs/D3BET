using System.Windows;
using BettingApp.Wpf.ViewModels;
using BettingApp.Wpf.Views;

namespace BettingApp.Wpf.Services;

public sealed class CustomerDisplayWindowService
{
    private CustomerDisplayWindow? currentWindow;

    public Task ShowAsync(MainViewModel viewModel)
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

        currentWindow = new CustomerDisplayWindow(viewModel);
        currentWindow.Closed += (_, _) => currentWindow = null;
        currentWindow.Show();
        currentWindow.Activate();

        return Task.CompletedTask;
    }
}
