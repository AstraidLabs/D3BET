using System.Globalization;
using BettingApp.Wpf.Commands;

namespace BettingApp.Wpf.ViewModels;

public sealed class MarketEditorViewModel : ObservableObject
{
    private Guid? editingMarketId;
    private string eventName = string.Empty;
    private string openingOdds = string.Empty;
    private bool isActive = true;

    public MarketEditorViewModel(Func<Task> saveAsync, Func<Task> cancelAsync)
    {
        SaveCommand = new AsyncRelayCommand(saveAsync);
        CancelCommand = new AsyncRelayCommand(cancelAsync);
    }

    public event EventHandler? CloseRequested;

    public string EventName
    {
        get => eventName;
        set => SetProperty(ref eventName, value);
    }

    public string OpeningOdds
    {
        get => openingOdds;
        set => SetProperty(ref openingOdds, value);
    }

    public bool IsActive
    {
        get => isActive;
        set => SetProperty(ref isActive, value);
    }

    public bool IsEditing => editingMarketId.HasValue;

    public Guid? EditingMarketId => editingMarketId;

    public string TitleText => IsEditing ? "Upravit vypsanou událost" : "Vypsat novou událost";

    public string SaveButtonText => IsEditing ? "Uložit událost" : "Vypsat událost";

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand CancelCommand { get; }

    public void BeginCreate()
    {
        editingMarketId = null;
        EventName = string.Empty;
        OpeningOdds = string.Empty;
        IsActive = true;
        RefreshState();
    }

    public void BeginEdit(MarketItemViewModel market)
    {
        editingMarketId = market.Id;
        EventName = market.EventName;
        OpeningOdds = market.OpeningOdds.ToString("0.00", CultureInfo.InvariantCulture);
        IsActive = market.IsActive;
        RefreshState();
    }

    public bool TryParse(out decimal parsedOdds, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(EventName))
        {
            parsedOdds = 0m;
            errorMessage = "Vyplňte název události.";
            return false;
        }

        var normalized = OpeningOdds.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedOdds) || parsedOdds <= 1m)
        {
            errorMessage = "Výchozí kurz musí být číslo větší než 1.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public void RequestClose()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshState()
    {
        RaisePropertyChanged(nameof(IsEditing));
        RaisePropertyChanged(nameof(EditingMarketId));
        RaisePropertyChanged(nameof(TitleText));
        RaisePropertyChanged(nameof(SaveButtonText));
    }
}
