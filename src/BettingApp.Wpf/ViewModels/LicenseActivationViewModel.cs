using System.Globalization;
using BettingApp.Wpf.Commands;
using BettingApp.Wpf.Services;

namespace BettingApp.Wpf.ViewModels;

public sealed class LicenseActivationViewModel : ObservableObject
{
    private readonly Func<string, string, CancellationToken, Task<ClientLicenseState>> activateAsync;
    private string email = "client@d3bet.local";
    private string activationKeyBase64 = "RDNCRVQtU0lOR0xFLUxJQ0VOU0UtMjAyNg==";
    private string statusMessage = "Zadejte licenční e-mail a klíč pro propojení klienta se serverem D3Bet.";
    private bool isBusy;

    public LicenseActivationViewModel(Func<string, string, CancellationToken, Task<ClientLicenseState>> activateAsync)
    {
        this.activateAsync = activateAsync;
        ActivateCommand = new AsyncRelayCommand(ActivateInternalAsync, () => !IsBusy);
    }

    public ClientLicenseState? ActivatedLicense { get; private set; }

    public string Email
    {
        get => email;
        set => SetProperty(ref email, value);
    }

    public string ActivationKeyBase64
    {
        get => activationKeyBase64;
        set => SetProperty(ref activationKeyBase64, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value))
            {
                ActivateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand ActivateCommand { get; }

    public async Task ActivateInternalAsync()
    {
        try
        {
            IsBusy = true;
            ActivatedLicense = await activateAsync(Email.Trim(), ActivationKeyBase64.Trim(), CancellationToken.None);
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                "Licence je aktivní do {0:g}. Klient může bezpečně načíst konfiguraci serveru.",
                ActivatedLicense.ExpiresAtUtc.ToLocalTime());
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ActivatedLicense = null;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
