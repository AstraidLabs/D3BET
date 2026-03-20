using System.Windows;

namespace BettingApp.Wpf.Services;

public sealed class ConfirmationDialogService
{
    public bool Confirm(string title, string message)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }
}
