using System.Windows;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public LoginViewModel ViewModel { get; }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ViewModel.LoginAsync(LoginPasswordBox.Password))
        {
            DialogResult = true;
            Close();
        }
    }

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RegisterAsync(RegisterPasswordBox.Password, RegisterConfirmPasswordBox.Password);
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ActivateAsync();
    }

    private async void ReactivateButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ReactivateAsync();
    }

    private async void ForgotPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ForgotPasswordAsync();
    }

    private async void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ResetPasswordAsync(ResetPasswordBox.Password, ResetConfirmPasswordBox.Password);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
