using System.Windows;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class LicenseActivationWindow : Window
{
    public LicenseActivationWindow(LicenseActivationViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public LicenseActivationViewModel ViewModel { get; }

    private async void ActivateAndClose(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy)
        {
            return;
        }

        await ViewModel.ActivateInternalAsync();
        if (ViewModel.ActivatedLicense is not null)
        {
            DialogResult = true;
            Close();
        }
    }
}
