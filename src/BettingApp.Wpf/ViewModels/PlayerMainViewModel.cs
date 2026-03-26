using System.Collections.ObjectModel;
using System.Globalization;
using BettingApp.Wpf.Commands;
using BettingApp.Wpf.Services;

namespace BettingApp.Wpf.ViewModels;

public sealed class PlayerMainViewModel : ObservableObject, IAsyncViewModel
{
    private readonly PlayerApiClient playerApiClient;
    private readonly ProfileWindowService profileWindowService;
    private readonly OperatorAuthService operatorAuthService;
    private readonly OperatorSessionContext operatorSessionContext;

    private string statusMessage = "Načítáme hráčský dashboard D3Bet.";
    private string welcomeMessage = "Hráčský účet je připravený pro sázení.";
    private string walletSummary = "Peněženka se načítá.";
    private string quoteSummary = "Vyberte událost a zadejte vklad v D3Kreditu.";
    private string topUpAmount = "500";
    private string stakeAmount = "50";
    private bool isBusy;
    private PlayerMarketItemViewModel? selectedMarket;

    public PlayerMainViewModel(
        PlayerApiClient playerApiClient,
        ProfileWindowService profileWindowService,
        OperatorAuthService operatorAuthService,
        OperatorSessionContext operatorSessionContext)
    {
        this.playerApiClient = playerApiClient;
        this.profileWindowService = profileWindowService;
        this.operatorAuthService = operatorAuthService;
        this.operatorSessionContext = operatorSessionContext;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        TopUpCommand = new AsyncRelayCommand(TopUpAsync);
        PlaceBetCommand = new AsyncRelayCommand(PlaceBetAsync, () => SelectedMarket is not null);
        OpenProfileCommand = new AsyncRelayCommand(OpenProfileAsync);
        SwitchAccountCommand = new AsyncRelayCommand(SwitchAccountAsync);
    }

    public ObservableCollection<PlayerMarketItemViewModel> Markets { get; } = new();

    public ObservableCollection<PlayerBetItemViewModel> RecentBets { get; } = new();

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public string WelcomeMessage
    {
        get => welcomeMessage;
        set => SetProperty(ref welcomeMessage, value);
    }

    public string WalletSummary
    {
        get => walletSummary;
        set => SetProperty(ref walletSummary, value);
    }

    public string QuoteSummary
    {
        get => quoteSummary;
        set => SetProperty(ref quoteSummary, value);
    }

    public string TopUpAmount
    {
        get => topUpAmount;
        set => SetProperty(ref topUpAmount, value);
    }

    public string StakeAmount
    {
        get => stakeAmount;
        set
        {
            if (SetProperty(ref stakeAmount, value))
            {
                _ = RefreshQuoteAsync();
            }
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public PlayerMarketItemViewModel? SelectedMarket
    {
        get => selectedMarket;
        set
        {
            if (SetProperty(ref selectedMarket, value))
            {
                PlaceBetCommand.RaiseCanExecuteChanged();
                _ = RefreshQuoteAsync();
            }
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand TopUpCommand { get; }

    public AsyncRelayCommand PlaceBetCommand { get; }

    public AsyncRelayCommand OpenProfileCommand { get; }

    public AsyncRelayCommand SwitchAccountCommand { get; }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    public Task ShutdownAsync() => Task.CompletedTask;

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            var dashboard = await playerApiClient.GetDashboardAsync();
            var culture = CultureInfo.CurrentCulture;

            WelcomeMessage = $"Vítejte, {dashboard.Profile.UserName}. Můžete sázet na aktivní události a spravovat svůj {dashboard.Wallet.CreditCode}.";
            WalletSummary = $"Zůstatek: {dashboard.Wallet.Balance:0.00} {dashboard.Wallet.CreditCode} | kurz zpět {dashboard.Wallet.LastCreditToMoneyRate:0.0000}";

            var selectedMarketId = SelectedMarket?.MarketId;
            Markets.Clear();
            foreach (var market in dashboard.Markets)
            {
                Markets.Add(new PlayerMarketItemViewModel
                {
                    MarketId = market.MarketId,
                    EventName = market.EventName,
                    OddsDisplay = market.CurrentOdds.ToString("0.00", culture),
                    StatusDisplay = market.IsActive ? "Aktivní pro sázení" : "Momentálně uzavřená",
                    IsActive = market.IsActive
                });
            }

            SelectedMarket = Markets.FirstOrDefault(x => x.MarketId == selectedMarketId) ?? Markets.FirstOrDefault(x => x.IsActive);

            RecentBets.Clear();
            foreach (var bet in dashboard.RecentBets)
            {
                RecentBets.Add(new PlayerBetItemViewModel
                {
                    EventName = bet.EventName,
                    OddsDisplay = bet.Odds.ToString("0.00", culture),
                    StakeDisplay = $"{bet.Stake:0.00} {bet.StakeCurrencyCode} ({bet.StakeRealMoneyEquivalent.ToString("C", culture)})",
                    PotentialPayoutDisplay = $"{bet.PotentialPayout:0.00} {bet.StakeCurrencyCode}",
                    OutcomeDisplay = bet.OutcomeStatus.ToString(),
                    PlacedAtDisplay = bet.PlacedAtUtc.ToLocalTime().ToString("g", culture)
                });
            }

            await RefreshQuoteAsync();
            StatusMessage = "Hráčský dashboard je synchronizovaný se serverem.";
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

    private async Task RefreshQuoteAsync()
    {
        if (SelectedMarket is null || !SelectedMarket.IsActive)
        {
            QuoteSummary = "Vyberte aktivní událost pro výpočet možného výnosu.";
            return;
        }

        if (!decimal.TryParse(StakeAmount.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var stake) || stake <= 0)
        {
            QuoteSummary = "Zadejte platný vklad v D3Kreditu.";
            return;
        }

        try
        {
            var quote = await playerApiClient.GetQuoteAsync(SelectedMarket.MarketId, stake);
            QuoteSummary = $"Kurz: 1 {quote.CreditCode} = {quote.CreditToMoneyRate:0.0000} {quote.RealCurrencyCode} | multiplikátor {quote.MarketParticipationMultiplier:0.0000} | možná výhra {quote.PotentialPayoutCredits:0.00} {quote.CreditCode}";
        }
        catch (Exception ex)
        {
            QuoteSummary = ex.Message;
        }
    }

    private async Task TopUpAsync()
    {
        if (!decimal.TryParse(TopUpAmount.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            StatusMessage = "Částka dobití musí být větší než 0.";
            return;
        }

        try
        {
            IsBusy = true;
            var topUp = await playerApiClient.TopUpAsync(amount);
            StatusMessage = $"Dobití proběhlo. Připsáno {topUp.AddedCredits:0.00} {topUp.CreditCode}.";
            await LoadAsync();
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

    private async Task PlaceBetAsync()
    {
        if (SelectedMarket is null)
        {
            StatusMessage = "Vyberte událost, na kterou chcete sázet.";
            return;
        }

        if (!decimal.TryParse(StakeAmount.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var stake) || stake <= 0)
        {
            StatusMessage = "Vklad musí být větší než 0 D3Kreditu.";
            return;
        }

        try
        {
            IsBusy = true;
            var placement = await playerApiClient.PlaceBetAsync(SelectedMarket.MarketId, stake);
            StatusMessage = $"Sázka byla přijata s kurzem {placement.AppliedOdds:0.00}. Nový zůstatek byl aktualizován.";
            await LoadAsync();
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

    private async Task OpenProfileAsync()
    {
        if (await profileWindowService.ShowAsync())
        {
            await LoadAsync();
        }
    }

    private async Task SwitchAccountAsync()
    {
        await operatorAuthService.ForceReauthenticateAsync(CancellationToken.None);
        WelcomeMessage = $"Vítejte, {operatorSessionContext.DisplayName}.";
        await LoadAsync();
    }
}

public sealed class PlayerMarketItemViewModel
{
    public Guid MarketId { get; init; }

    public string EventName { get; init; } = string.Empty;

    public string OddsDisplay { get; init; } = string.Empty;

    public string StatusDisplay { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class PlayerBetItemViewModel
{
    public string EventName { get; init; } = string.Empty;

    public string OddsDisplay { get; init; } = string.Empty;

    public string StakeDisplay { get; init; } = string.Empty;

    public string PotentialPayoutDisplay { get; init; } = string.Empty;

    public string OutcomeDisplay { get; init; } = string.Empty;

    public string PlacedAtDisplay { get; init; } = string.Empty;
}
