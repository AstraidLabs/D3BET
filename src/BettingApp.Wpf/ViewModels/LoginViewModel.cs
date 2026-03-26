using BettingApp.Wpf.Services;

namespace BettingApp.Wpf.ViewModels;

public sealed class LoginViewModel(
    SelfServiceApiClient selfServiceApiClient,
    Func<string, string, CancellationToken, Task<OperatorSessionData>> loginAsync) : ObservableObject
{
    private string loginUserName = string.Empty;
    private string registerUserName = string.Empty;
    private string registerEmail = string.Empty;
    private string activationUserNameOrEmail = string.Empty;
    private string activationToken = string.Empty;
    private string reactivationUserNameOrEmail = string.Empty;
    private string forgotUserNameOrEmail = string.Empty;
    private string resetUserNameOrEmail = string.Empty;
    private string resetToken = string.Empty;
    private string statusTitle = "Desktop přihlášení";
    private string statusMessage = "Přihlášení, registrace i obnova účtu probíhají přímo ve WPF bez otevření prohlížeče.";
    private string previewText = string.Empty;
    private bool isBusy;
    private int selectedTabIndex;

    public OperatorSessionData? AuthenticatedSession { get; private set; }

    public string LoginUserName
    {
        get => loginUserName;
        set => SetProperty(ref loginUserName, value);
    }

    public string RegisterUserName
    {
        get => registerUserName;
        set => SetProperty(ref registerUserName, value);
    }

    public string RegisterEmail
    {
        get => registerEmail;
        set => SetProperty(ref registerEmail, value);
    }

    public string ActivationUserNameOrEmail
    {
        get => activationUserNameOrEmail;
        set => SetProperty(ref activationUserNameOrEmail, value);
    }

    public string ActivationToken
    {
        get => activationToken;
        set => SetProperty(ref activationToken, value);
    }

    public string ReactivationUserNameOrEmail
    {
        get => reactivationUserNameOrEmail;
        set => SetProperty(ref reactivationUserNameOrEmail, value);
    }

    public string ForgotUserNameOrEmail
    {
        get => forgotUserNameOrEmail;
        set => SetProperty(ref forgotUserNameOrEmail, value);
    }

    public string ResetUserNameOrEmail
    {
        get => resetUserNameOrEmail;
        set => SetProperty(ref resetUserNameOrEmail, value);
    }

    public string ResetToken
    {
        get => resetToken;
        set => SetProperty(ref resetToken, value);
    }

    public string StatusTitle
    {
        get => statusTitle;
        private set => SetProperty(ref statusTitle, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string PreviewText
    {
        get => previewText;
        private set => SetProperty(ref previewText, value);
    }

    public bool HasPreview => !string.IsNullOrWhiteSpace(PreviewText);

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public int SelectedTabIndex
    {
        get => selectedTabIndex;
        set => SetProperty(ref selectedTabIndex, value);
    }

    public async Task<bool> LoginAsync(string password, CancellationToken cancellationToken = default)
    {
        try
        {
            SetBusy();
            ClearPreview();
            AuthenticatedSession = await loginAsync(LoginUserName.Trim(), password, cancellationToken);
            SetStatus("Přihlášení proběhlo", $"Účet {AuthenticatedSession.UserName} je připravený pro další práci v D3Bet.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("Přihlášení se nepodařilo", ex.Message);
            return false;
        }
        finally
        {
            ClearBusy();
        }
    }

    public async Task RegisterAsync(string password, string confirmPassword, CancellationToken cancellationToken = default)
    {
        await RunSelfServiceAsync(
            async () => await selfServiceApiClient.RegisterAsync(RegisterUserName.Trim(), RegisterEmail.Trim(), password, confirmPassword, cancellationToken),
            response =>
            {
                ActivationUserNameOrEmail = string.IsNullOrWhiteSpace(RegisterEmail) ? RegisterUserName.Trim() : RegisterEmail.Trim();
                if (!string.IsNullOrWhiteSpace(response.Preview?.Token))
                {
                    ActivationToken = response.Preview.Token;
                }

                SelectedTabIndex = 2;
                return "Registrace dokončena";
            });
    }

    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        await RunSelfServiceAsync(
            async () => await selfServiceApiClient.ActivateAsync(ActivationUserNameOrEmail.Trim(), ActivationToken.Trim(), cancellationToken),
            _ => "Účet byl aktivován");
    }

    public async Task ReactivateAsync(CancellationToken cancellationToken = default)
    {
        await RunSelfServiceAsync(
            async () => await selfServiceApiClient.ReactivateAsync(ReactivationUserNameOrEmail.Trim(), cancellationToken),
            response =>
            {
                ActivationUserNameOrEmail = ReactivationUserNameOrEmail.Trim();
                if (!string.IsNullOrWhiteSpace(response.Preview?.Token))
                {
                    ActivationToken = response.Preview.Token;
                }

                SelectedTabIndex = 2;
                return "Aktivace byla obnovena";
            });
    }

    public async Task ForgotPasswordAsync(CancellationToken cancellationToken = default)
    {
        await RunSelfServiceAsync(
            async () => await selfServiceApiClient.ForgotPasswordAsync(ForgotUserNameOrEmail.Trim(), cancellationToken),
            response =>
            {
                ResetUserNameOrEmail = ForgotUserNameOrEmail.Trim();
                if (!string.IsNullOrWhiteSpace(response.Preview?.Token))
                {
                    ResetToken = response.Preview.Token;
                }

                SelectedTabIndex = 4;
                return "Obnova hesla byla připravena";
            });
    }

    public async Task ResetPasswordAsync(string newPassword, string confirmPassword, CancellationToken cancellationToken = default)
    {
        await RunSelfServiceAsync(
            async () => await selfServiceApiClient.ResetPasswordAsync(ResetUserNameOrEmail.Trim(), ResetToken.Trim(), newPassword, confirmPassword, cancellationToken),
            _ => "Heslo bylo změněno");
    }

    private async Task RunSelfServiceAsync(
        Func<Task<SelfServiceActionResponse>> action,
        Func<SelfServiceActionResponse, string> titleFactory)
    {
        try
        {
            SetBusy();
            var response = await action();
            SetStatus(titleFactory(response), response.Message);
            SetPreview(response.Preview);
        }
        catch (Exception ex)
        {
            SetStatus("Akci se nepodařilo dokončit", ex.Message);
            ClearPreview();
        }
        finally
        {
            ClearBusy();
        }
    }

    private void SetStatus(string title, string message)
    {
        StatusTitle = title;
        StatusMessage = message;
    }

    private void SetPreview(SelfServicePreviewResponse? preview)
    {
        PreviewText = preview is null
            ? string.Empty
            : $"Typ: {preview.Purpose}{Environment.NewLine}Účet: {preview.UserNameOrEmail}{Environment.NewLine}Token: {preview.Token}{Environment.NewLine}Link: {preview.Link}";
        RaisePropertyChanged(nameof(HasPreview));
    }

    private void ClearPreview()
    {
        PreviewText = string.Empty;
        RaisePropertyChanged(nameof(HasPreview));
    }

    private void SetBusy() => IsBusy = true;

    private void ClearBusy() => IsBusy = false;
}
