using System.Collections.ObjectModel;
using System.Globalization;
using BettingApp.Wpf.Commands;

namespace BettingApp.Wpf.ViewModels;

public sealed class BetEditorViewModel : ObservableObject
{
    private BetEditorMode mode = BetEditorMode.Create;
    private Guid? editingBetId;
    private BettorOptionViewModel? selectedBettor;
    private MarketOptionViewModel? selectedMarket;
    private string newBettorName = string.Empty;
    private string eventNamePreview = string.Empty;
    private string oddsPreview = string.Empty;
    private string stake = string.Empty;
    private bool isCommissionFeePaid;

    public BetEditorViewModel(Func<Task> saveAsync, Func<Task> cancelAsync)
    {
        SaveCommand = new AsyncRelayCommand(saveAsync);
        CancelEditCommand = new AsyncRelayCommand(cancelAsync);
    }

    public ObservableCollection<BettorOptionViewModel> Bettors { get; } = new();

    public ObservableCollection<MarketOptionViewModel> Markets { get; } = new();

    public event EventHandler? CloseRequested;

    public BettorOptionViewModel? SelectedBettor
    {
        get => selectedBettor;
        set => SetProperty(ref selectedBettor, value);
    }

    public string NewBettorName
    {
        get => newBettorName;
        set => SetProperty(ref newBettorName, value);
    }

    public string EventName
    {
        get => eventNamePreview;
        private set => SetProperty(ref eventNamePreview, value);
    }

    public string Odds
    {
        get => oddsPreview;
        private set => SetProperty(ref oddsPreview, value);
    }

    public MarketOptionViewModel? SelectedMarket
    {
        get => selectedMarket;
        set
        {
            if (SetProperty(ref selectedMarket, value))
            {
                EventName = value?.EventName ?? string.Empty;
                Odds = value?.CurrentOddsDisplay ?? string.Empty;
            }
        }
    }

    public string Stake
    {
        get => stake;
        set => SetProperty(ref stake, value);
    }

    public bool IsCommissionFeePaid
    {
        get => isCommissionFeePaid;
        set => SetProperty(ref isCommissionFeePaid, value);
    }

    public bool IsEditing => editingBetId.HasValue;

    public string TitleText => mode switch
    {
        BetEditorMode.Edit => "Upravit sázku",
        BetEditorMode.AddBettor => "Přidat tipéra",
        _ => "Nová sázka"
    };

    public string SubtitleText => mode switch
    {
        BetEditorMode.Edit => "Upravte parametry tiketu v samostatném editoru bez rušení hlavního přehledu.",
        BetEditorMode.AddBettor => "Přidejte dalšího sázejícího na stejný tip rychle a přehledně.",
        _ => "Založte nový tiket v odděleném formuláři, který vás nebude vytrhávat z práce se seznamem."
    };

    public string SaveButtonText => mode switch
    {
        BetEditorMode.Edit => "Uložit úpravy",
        BetEditorMode.AddBettor => "Přidat tipéra",
        _ => "Přijmout sázku"
    };

    public Guid? EditingBetId => editingBetId;

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand CancelEditCommand { get; }

    public void SetBettors(IEnumerable<BettorOptionViewModel> bettors)
    {
        var selectedBettorId = SelectedBettor?.Id;

        Bettors.Clear();
        foreach (var bettor in bettors)
        {
            Bettors.Add(bettor);
        }

        SelectedBettor = selectedBettorId.HasValue
            ? Bettors.FirstOrDefault(x => x.Id == selectedBettorId.Value)
            : null;
    }

    public void SetMarkets(IEnumerable<MarketOptionViewModel> markets)
    {
        var selectedMarketId = SelectedMarket?.Id;

        Markets.Clear();
        foreach (var market in markets.Where(x => x.IsActive || selectedMarketId == x.Id))
        {
            Markets.Add(market);
        }

        SelectedMarket = selectedMarketId.HasValue
            ? Markets.FirstOrDefault(x => x.Id == selectedMarketId.Value)
            : Markets.FirstOrDefault();
    }

    public void BeginEdit(BetItemViewModel bet)
    {
        mode = BetEditorMode.Edit;
        editingBetId = bet.Id;
        SelectedMarket = bet.BettingMarketId.HasValue
            ? Markets.FirstOrDefault(x => x.Id == bet.BettingMarketId.Value)
            : Markets.FirstOrDefault(x => string.Equals(x.EventName, bet.EventName, StringComparison.CurrentCulture));
        EventName = bet.EventName;
        Odds = bet.Odds.ToString("0.00", CultureInfo.InvariantCulture);
        Stake = bet.Stake.ToString("0.00", CultureInfo.InvariantCulture);
        NewBettorName = string.Empty;
        SelectedBettor = Bettors.FirstOrDefault(x => x.Id == bet.BettorId);
        IsCommissionFeePaid = bet.IsCommissionFeePaid;
        RefreshState();
    }

    public void PrepareAdditionalBettor(BetItemViewModel bet)
    {
        mode = BetEditorMode.AddBettor;
        editingBetId = null;
        SelectedMarket = bet.BettingMarketId.HasValue
            ? Markets.FirstOrDefault(x => x.Id == bet.BettingMarketId.Value)
            : Markets.FirstOrDefault(x => string.Equals(x.EventName, bet.EventName, StringComparison.CurrentCulture));
        EventName = bet.EventName;
        Odds = bet.Odds.ToString("0.00", CultureInfo.InvariantCulture);
        Stake = string.Empty;
        NewBettorName = string.Empty;
        SelectedBettor = null;
        IsCommissionFeePaid = false;
        RefreshState();
    }

    public void Reset()
    {
        mode = BetEditorMode.Create;
        editingBetId = null;
        EventName = string.Empty;
        Odds = string.Empty;
        Stake = string.Empty;
        NewBettorName = string.Empty;
        SelectedBettor = null;
        SelectedMarket = Markets.FirstOrDefault();
        IsCommissionFeePaid = false;
        RefreshState();
    }

    public bool TryParseValues(out Guid selectedMarketId, out decimal parsedStake, out string? errorMessage)
    {
        if (SelectedMarket is null)
        {
            selectedMarketId = Guid.Empty;
            parsedStake = default;
            errorMessage = "Vyberte vypsanou událost.";
            return false;
        }

        if (!TryParseDecimal(Stake, out parsedStake))
        {
            selectedMarketId = Guid.Empty;
            errorMessage = "Částka není ve správném formátu.";
            return false;
        }

        selectedMarketId = SelectedMarket.Id;
        errorMessage = null;
        return true;
    }

    public void BeginCreate()
    {
        Reset();
    }

    public void RequestClose()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshState()
    {
        RaisePropertyChanged(nameof(IsEditing));
        RaisePropertyChanged(nameof(TitleText));
        RaisePropertyChanged(nameof(SubtitleText));
        RaisePropertyChanged(nameof(SaveButtonText));
    }

    private static bool TryParseDecimal(string input, out decimal value)
    {
        var normalized = input.Trim().Replace(',', '.');
        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out value);
    }
}

public enum BetEditorMode
{
    Create,
    Edit,
    AddBettor
}
