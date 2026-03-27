using System.Collections.ObjectModel;
using System.Globalization;
using BettingApp.Domain.Entities;
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
    private string withdrawalAmount = "100";
    private string withdrawalSummary = "Výběr do měny zatím nebyl připraven.";
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
        RequestWithdrawalCommand = new AsyncRelayCommand(RequestWithdrawalAsync);
        PlaceBetCommand = new AsyncRelayCommand(PlaceBetAsync, () => SelectedMarket is not null);
        OpenProfileCommand = new AsyncRelayCommand(OpenProfileAsync);
        SwitchAccountCommand = new AsyncRelayCommand(SwitchAccountAsync);
    }

    public ObservableCollection<PlayerMarketItemViewModel> Markets { get; } = new();

    public ObservableCollection<PlayerBetItemViewModel> RecentBets { get; } = new();

    public ObservableCollection<PlayerWithdrawalItemViewModel> RecentWithdrawals { get; } = new();

    public ObservableCollection<PlayerReceiptItemViewModel> RecentReceipts { get; } = new();

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

    public string WithdrawalAmount
    {
        get => withdrawalAmount;
        set => SetProperty(ref withdrawalAmount, value);
    }

    public string WithdrawalSummary
    {
        get => withdrawalSummary;
        set => SetProperty(ref withdrawalSummary, value);
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

    public AsyncRelayCommand RequestWithdrawalCommand { get; }

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
            WithdrawalSummary = $"Při výběru se použije aktuální kurz {dashboard.Wallet.LastCreditToMoneyRate:0.0000} CZK za 1 {dashboard.Wallet.CreditCode}.";

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

            RecentWithdrawals.Clear();
            foreach (var withdrawal in dashboard.RecentWithdrawals)
            {
                RecentWithdrawals.Add(new PlayerWithdrawalItemViewModel
                {
                    RequestedAtDisplay = withdrawal.RequestedAtUtc.ToLocalTime().ToString("g", culture),
                    AmountDisplay = $"{withdrawal.CreditAmount:0.00} D3Kredit -> {withdrawal.RealMoneyAmount:0.00} {withdrawal.RealCurrencyCode}",
                    StatusDisplay = withdrawal.Status switch
                    {
                        CreditWithdrawalRequestStatus.Paid => "Vyplaceno",
                        CreditWithdrawalRequestStatus.Rejected => "Zamítnuto",
                        CreditWithdrawalRequestStatus.Cancelled => "Zrušeno",
                        _ => "Čeká na zpracování"
                    },
                    ReasonDisplay = string.IsNullOrWhiteSpace(withdrawal.ProcessedReason)
                        ? withdrawal.Reason
                        : $"{withdrawal.Reason} | {withdrawal.ProcessedReason}"
                });
            }

            RecentReceipts.Clear();
            foreach (var receipt in dashboard.RecentReceipts)
            {
                RecentReceipts.Add(new PlayerReceiptItemViewModel
                {
                    Title = receipt.Title,
                    DocumentNumber = receipt.DocumentNumber,
                    AmountDisplay = $"{receipt.CreditAmount:0.00} D3Kredit | {receipt.RealMoneyAmount:0.00} {receipt.RealCurrencyCode}",
                    Summary = receipt.Summary,
                    IssuedAtDisplay = receipt.IssuedAtUtc.ToLocalTime().ToString("g", culture)
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
            StatusMessage = topUp.IssuedReceipt is null
                ? $"Dobití proběhlo. Připsáno {topUp.AddedCredits:0.00} {topUp.CreditCode}."
                : $"Dobití proběhlo. Připsáno {topUp.AddedCredits:0.00} {topUp.CreditCode}. Doklad: {topUp.IssuedReceipt.DocumentNumber}.";
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

    private async Task RequestWithdrawalAsync()
    {
        if (!decimal.TryParse(WithdrawalAmount.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            StatusMessage = "Částka výběru musí být větší než 0 D3Kreditu.";
            return;
        }

        try
        {
            IsBusy = true;
            var withdrawal = await playerApiClient.RequestWithdrawalAsync(amount, "CZK", "Výběr kreditu hráčem.");
            StatusMessage = withdrawal.Status == CreditWithdrawalRequestStatus.Paid
                ? withdrawal.IssuedReceipt is null
                    ? $"Výběr byl zpracovaný. Převádí se {withdrawal.RealMoneyAmount:0.00} {withdrawal.RealCurrencyCode}."
                    : $"Výběr byl zpracovaný. Převádí se {withdrawal.RealMoneyAmount:0.00} {withdrawal.RealCurrencyCode}. Doklad: {withdrawal.IssuedReceipt.DocumentNumber}."
                : $"Žádost o výběr byla založená. Připravuje se převod {withdrawal.RealMoneyAmount:0.00} {withdrawal.RealCurrencyCode}.";
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

public sealed class PlayerWithdrawalItemViewModel
{
    public string RequestedAtDisplay { get; init; } = string.Empty;

    public string AmountDisplay { get; init; } = string.Empty;

    public string StatusDisplay { get; init; } = string.Empty;

    public string ReasonDisplay { get; init; } = string.Empty;
}

public sealed class PlayerReceiptItemViewModel
{
    public string Title { get; init; } = string.Empty;

    public string DocumentNumber { get; init; } = string.Empty;

    public string AmountDisplay { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string IssuedAtDisplay { get; init; } = string.Empty;
}
