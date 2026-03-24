using System.Windows;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel viewModel;
    private TaskCompletionSource<LoginCredentials>? loginCompletionSource;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        UserNameTextBox.Focus();
    }

    public Task<LoginCredentials> WaitForCredentialsAsync(CancellationToken cancellationToken)
    {
        loginCompletionSource = new TaskCompletionSource<LoginCredentials>();
        cancellationToken.Register(() => loginCompletionSource.TrySetCanceled());
        return loginCompletionSource.Task;
    }

    public void ShowError(string message)
    {
        viewModel.ErrorMessage = message;
        viewModel.IsLoggingIn = false;
        LoginButton.IsEnabled = true;
        PasswordBox.Focus();
    }

    private void OnLoginButtonClick(object sender, RoutedEventArgs e)
    {
        viewModel.ErrorMessage = null;
        viewModel.IsLoggingIn = true;
        LoginButton.IsEnabled = false;

        var credentials = new LoginCredentials(viewModel.UserName, PasswordBox.Password);
        loginCompletionSource?.TrySetResult(credentials);
    }
}

public sealed record LoginCredentials(string UserName, string Password);
