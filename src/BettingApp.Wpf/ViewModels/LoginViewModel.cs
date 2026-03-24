using System.Windows.Input;

namespace BettingApp.Wpf.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private string userName = string.Empty;
    private string? errorMessage;
    private bool isLoggingIn;

    public string UserName
    {
        get => userName;
        set => SetProperty(ref userName, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        set
        {
            if (SetProperty(ref errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsLoggingIn
    {
        get => isLoggingIn;
        set => SetProperty(ref isLoggingIn, value);
    }
}
