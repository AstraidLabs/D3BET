using System.Collections.ObjectModel;
using BettingApp.Wpf.Commands;

namespace BettingApp.Wpf.ViewModels;

public sealed class LicenseAdministrationViewModel : ObservableObject
{
    private string serverInstanceId = "Neznámý server";
    private string overviewSummary = "Licenční přehled se načítá.";
    private string selectedLicenseSummary = "Vyberte licenci, se kterou chcete pracovat.";
    private string adminReason = "Provozní zásah administrátora.";
    private string extendDays = "30";
    private LicenseAdminItemViewModel? selectedLicense;

    public LicenseAdministrationViewModel(
        Func<Task> refreshAsync,
        Func<Task> revokeAsync,
        Func<Task> restoreAsync,
        Func<Task> releaseAsync,
        Func<Task> extendAsync)
    {
        RefreshCommand = new AsyncRelayCommand(refreshAsync);
        RevokeCommand = new AsyncRelayCommand(revokeAsync, () => SelectedLicense is not null && !SelectedLicense.IsRevoked);
        RestoreCommand = new AsyncRelayCommand(restoreAsync, () => SelectedLicense is not null && SelectedLicense.IsRevoked);
        ReleaseCommand = new AsyncRelayCommand(releaseAsync, () => SelectedLicense is not null);
        ExtendCommand = new AsyncRelayCommand(extendAsync, () => SelectedLicense is not null);
    }

    public ObservableCollection<LicenseAdminItemViewModel> Licenses { get; } = new();

    public ObservableCollection<LicenseAuditEntryViewModel> AuditEntries { get; } = new();

    public string ServerInstanceId
    {
        get => serverInstanceId;
        set => SetProperty(ref serverInstanceId, value);
    }

    public string OverviewSummary
    {
        get => overviewSummary;
        set => SetProperty(ref overviewSummary, value);
    }

    public string SelectedLicenseSummary
    {
        get => selectedLicenseSummary;
        set => SetProperty(ref selectedLicenseSummary, value);
    }

    public string AdminReason
    {
        get => adminReason;
        set => SetProperty(ref adminReason, value);
    }

    public string ExtendDays
    {
        get => extendDays;
        set => SetProperty(ref extendDays, value);
    }

    public LicenseAdminItemViewModel? SelectedLicense
    {
        get => selectedLicense;
        set
        {
            if (SetProperty(ref selectedLicense, value))
            {
                SelectedLicenseSummary = value is null
                    ? "Vyberte licenci, se kterou chcete pracovat."
                    : $"{value.CustomerName} | {value.StatusLabel} | Instalace: {value.InstallationIdDisplay}";
                RevokeCommand.RaiseCanExecuteChanged();
                RestoreCommand.RaiseCanExecuteChanged();
                ReleaseCommand.RaiseCanExecuteChanged();
                ExtendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand RevokeCommand { get; }

    public AsyncRelayCommand RestoreCommand { get; }

    public AsyncRelayCommand ReleaseCommand { get; }

    public AsyncRelayCommand ExtendCommand { get; }
}

public sealed class LicenseAdminItemViewModel
{
    public string LicenseId { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string InstallationId { get; init; } = string.Empty;

    public string InstallationIdDisplay => string.IsNullOrWhiteSpace(InstallationId) ? "Zatím není navázaná" : InstallationId;

    public bool IsRevoked { get; init; }

    public bool IsExpiringSoon { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusBadgeText { get; init; } = string.Empty;

    public string IssuedAtDisplay { get; init; } = string.Empty;

    public string ExpiresAtDisplay { get; init; } = string.Empty;

    public string LastValidatedAtDisplay { get; init; } = string.Empty;
}

public sealed class LicenseAuditEntryViewModel
{
    public string TimestampDisplay { get; init; } = string.Empty;

    public string EventTypeDisplay { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string InstallationIdDisplay { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
