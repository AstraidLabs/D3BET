using System.Windows;
using BettingApp.Wpf.ViewModels;
using BettingApp.Wpf.Views;

namespace BettingApp.Wpf.Services;

public sealed class UserAdministrationWindowService
{
    private UserAdministrationWindow? currentWindow;

    public Task ShowAsync(UserAdministrationViewModel viewModel)
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

        currentWindow = new UserAdministrationWindow(viewModel);
        if (System.Windows.Application.Current.MainWindow is Window owner && owner != currentWindow)
        {
            currentWindow.Owner = owner;
        }

        currentWindow.Closed += (_, _) => currentWindow = null;
        currentWindow.Show();
        currentWindow.Activate();
        return Task.CompletedTask;
    }
}
