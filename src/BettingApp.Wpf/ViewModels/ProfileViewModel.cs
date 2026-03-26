using BettingApp.Wpf.Services;

namespace BettingApp.Wpf.ViewModels;

public sealed class ProfileViewModel(
    SelfServiceApiClient selfServiceApiClient,
    string accessToken) : ObservableObject
{
    private string userName = string.Empty;
    private string email = string.Empty;
    private string rolesDisplay = string.Empty;
    private string statusMessage = "Načítáme profil přihlášeného účtu.";
    private string previewText = string.Empty;
    private bool emailConfirmed;
    private bool isBusy;

    public bool WasSaved { get; private set; }

    public string UserName
    {
        get => userName;
        set => SetProperty(ref userName, value);
    }

    public string Email
    {
        get => email;
        set => SetProperty(ref email, value);
    }

    public string RolesDisplay
    {
        get => rolesDisplay;
        private set => SetProperty(ref rolesDisplay, value);
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

    public bool EmailConfirmed
    {
        get => emailConfirmed;
        private set => SetProperty(ref emailConfirmed, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            var profile = await selfServiceApiClient.GetProfileAsync(accessToken, cancellationToken);
            ApplyProfile(profile);
            StatusMessage = "Profil je připravený k úpravě.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            var response = await selfServiceApiClient.UpdateProfileAsync(accessToken, UserName.Trim(), Email.Trim(), cancellationToken);
            ApplyProfile(response.Profile);
            PreviewText = response.Preview is null
                ? string.Empty
                : $"Typ: {response.Preview.Purpose}{Environment.NewLine}Účet: {response.Preview.UserNameOrEmail}{Environment.NewLine}Token: {response.Preview.Token}{Environment.NewLine}Link: {response.Preview.Link}";
            RaisePropertyChanged(nameof(HasPreview));
            StatusMessage = response.Message;
            WasSaved = true;
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            PreviewText = string.Empty;
            RaisePropertyChanged(nameof(HasPreview));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyProfile(AccountProfileResponse profile)
    {
        UserName = profile.UserName;
        Email = profile.Email ?? string.Empty;
        EmailConfirmed = profile.EmailConfirmed;
        RolesDisplay = profile.Roles.Count == 0 ? "Bez role" : string.Join(", ", profile.Roles);
    }
}
