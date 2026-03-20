using System.Collections.ObjectModel;
using System.Globalization;
using BettingApp.Domain.Entities;
using BettingApp.Wpf.Commands;
using BettingApp.Wpf.Services;

namespace BettingApp.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncViewModel
{
    private readonly OperationsApiClient operationsApiClient;
    private readonly BettingRealtimeClient realtimeClient;
    private readonly OperatorAuthService operatorAuthService;
    private readonly BetEditorWindowService betEditorWindowService;
    private readonly CustomerDisplayWindowService customerDisplayWindowService;
    private readonly MarketEditorWindowService marketEditorWindowService;
    private readonly ConfirmationDialogService confirmationDialogService;
    private readonly OperatorSessionContext operatorSessionContext;
    private readonly ServerConnectionContext serverConnectionContext;
    private readonly SemaphoreSlim loadSemaphore = new(1, 1);
    private CancellationTokenSource? autoRefreshCts;
    private Task? autoRefreshTask;

    private string statusMessage = "Vše je připraveno. Můžete začít přijímat nové sázky.";
    private string analyticsSummary = "Načítáme aktuální přehled výkonu.";
    private string customerDisplayUpdatedAt = "Veřejný přehled se připravuje.";
    private string betSearchText = string.Empty;
    private BetStatusFilterOptionViewModel? selectedBetStatusFilter;
    private BetSortOptionViewModel? selectedBetSortOption;
    private int selectedPageSize;
    private int currentPage = 1;
    private string currentOperatorDisplayName = "Nepřihlášený provozovatel";
    private string currentOperatorRolesDisplay = "Bez role";
    private bool isAdmin;
    private bool isOperator;
    private bool isBusy;
    private string busyMessage = string.Empty;
    private Func<Task>? recoveryAction;
    private string recoveryActionLabel = "Zkusit znovu";
    private string auditSummary = "Auditní stopa se připravuje.";
    private string connectedServerName = "D3Bet Server";
    private string connectedServerDetail = "Výchozí adresa";

    public MainViewModel(
        OperationsApiClient operationsApiClient,
        BettingRealtimeClient realtimeClient,
        OperatorAuthService operatorAuthService,
        BetEditorWindowService betEditorWindowService,
        CustomerDisplayWindowService customerDisplayWindowService,
        MarketEditorWindowService marketEditorWindowService,
        ConfirmationDialogService confirmationDialogService,
        OperatorSessionContext operatorSessionContext,
        ServerConnectionContext serverConnectionContext)
    {
        this.operationsApiClient = operationsApiClient;
        this.realtimeClient = realtimeClient;
        this.operatorAuthService = operatorAuthService;
        this.betEditorWindowService = betEditorWindowService;
        this.customerDisplayWindowService = customerDisplayWindowService;
        this.marketEditorWindowService = marketEditorWindowService;
        this.confirmationDialogService = confirmationDialogService;
        this.operatorSessionContext = operatorSessionContext;
        this.serverConnectionContext = serverConnectionContext;

        Editor = new BetEditorViewModel(SaveAsync, CancelEditAsync);
        MarketEditor = new MarketEditorViewModel(SaveMarketAsync, CancelMarketEditAsync);
        Configuration = new AppConfigurationViewModel(SaveConfigurationAsync, ResetConfigurationToDefaults);
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        OpenNewBetEditorCommand = new AsyncRelayCommand(OpenNewBetEditorAsync, () => Markets.Any(market => market.IsActive));
        OpenNewMarketEditorCommand = new AsyncRelayCommand(OpenNewMarketEditorAsync, () => IsAdmin);
        OpenCustomerDisplayCommand = new AsyncRelayCommand(OpenCustomerDisplayAsync);
        SwitchOperatorCommand = new AsyncRelayCommand(SwitchOperatorAsync);
        RecoveryActionCommand = new AsyncRelayCommand(ExecuteRecoveryActionAsync, () => HasRecoveryAction && !IsBusy);
        DeleteBetCommand = new AsyncRelayCommand<BetItemViewModel>(DeleteBetAsync, bet => bet is not null);
        StartEditCommand = new AsyncRelayCommand<BetItemViewModel>(StartEditAsync, bet => bet is not null);
        StartEditMarketCommand = new AsyncRelayCommand<MarketItemViewModel>(StartEditMarketAsync, market => market is not null && IsAdmin);
        AddBettorToExistingBetCommand = new AsyncRelayCommand<BetItemViewModel>(AddBettorToExistingBetAsync, bet => bet is not null);
        MarkBetAsWonCommand = new AsyncRelayCommand<BetItemViewModel>(bet => ChangeOutcomeAsync(bet, BetOutcomeStatus.Won), bet => bet is not null && bet.OutcomeStatus != BetOutcomeStatus.Won);
        MarkBetAsLostCommand = new AsyncRelayCommand<BetItemViewModel>(bet => ChangeOutcomeAsync(bet, BetOutcomeStatus.Lost), bet => bet is not null && bet.OutcomeStatus != BetOutcomeStatus.Lost);
        ResetBetOutcomeCommand = new AsyncRelayCommand<BetItemViewModel>(bet => ChangeOutcomeAsync(bet, BetOutcomeStatus.Pending), bet => bet is not null && bet.OutcomeStatus != BetOutcomeStatus.Pending);
        PreviousPageCommand = new RelayCommand(GoToPreviousPage, () => CurrentPage > 1);
        NextPageCommand = new RelayCommand(GoToNextPage, () => CurrentPage < TotalPages);
        ResetBetFiltersCommand = new RelayCommand(ResetBetFilters);

        BetStatusFilters.Add(new BetStatusFilterOptionViewModel("all", "Vše"));
        BetStatusFilters.Add(new BetStatusFilterOptionViewModel("pending", "Čekající"));
        BetStatusFilters.Add(new BetStatusFilterOptionViewModel("winning", "Výherní"));

        BetSortOptions.Add(new BetSortOptionViewModel("placed_desc", "Nejnovější"));
        BetSortOptions.Add(new BetSortOptionViewModel("placed_asc", "Nejstarší"));
        BetSortOptions.Add(new BetSortOptionViewModel("stake_desc", "Nejvyšší vklad"));
        BetSortOptions.Add(new BetSortOptionViewModel("odds_desc", "Nejvyšší kurz"));
        BetSortOptions.Add(new BetSortOptionViewModel("event_asc", "Událost A-Z"));

        PageSizeOptions.Add(5);
        PageSizeOptions.Add(10);
        PageSizeOptions.Add(20);
        PageSizeOptions.Add(50);

        selectedBetStatusFilter = BetStatusFilters.First();
        selectedBetSortOption = BetSortOptions.First();
        selectedPageSize = 10;

        realtimeClient.BetCreated += HandleBetCreatedAsync;
    }

    public BetEditorViewModel Editor { get; }

    public MarketEditorViewModel MarketEditor { get; }

    public AppConfigurationViewModel Configuration { get; }

    public ObservableCollection<MarketItemViewModel> Markets { get; } = new();

    public ObservableCollection<BetItemViewModel> RecentBets { get; } = new();

    public ObservableCollection<BetItemViewModel> VisibleRecentBets { get; } = new();

    public ObservableCollection<DashboardKpiViewModel> DashboardKpis { get; } = new();

    public ObservableCollection<DashboardBarItemViewModel> TopBettors { get; } = new();

    public ObservableCollection<DashboardBarItemViewModel> EventDistribution { get; } = new();

    public ObservableCollection<DashboardTrendPointViewModel> BetVolumeTrend { get; } = new();

    public ObservableCollection<CustomerDisplayTileViewModel> CustomerDisplayTiles { get; } = new();

    public ObservableCollection<CustomerTickerItemViewModel> CustomerTickerItems { get; } = new();

    public ObservableCollection<AuditLogItemViewModel> AuditEntries { get; } = new();

    public ObservableCollection<BetStatusFilterOptionViewModel> BetStatusFilters { get; } = new();

    public ObservableCollection<BetSortOptionViewModel> BetSortOptions { get; } = new();

    public ObservableCollection<int> PageSizeOptions { get; } = new();

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public string AnalyticsSummary
    {
        get => analyticsSummary;
        set => SetProperty(ref analyticsSummary, value);
    }

    public string CustomerDisplayUpdatedAt
    {
        get => customerDisplayUpdatedAt;
        set => SetProperty(ref customerDisplayUpdatedAt, value);
    }

    public string AuditSummary
    {
        get => auditSummary;
        set => SetProperty(ref auditSummary, value);
    }

    public string ConnectedServerName
    {
        get => connectedServerName;
        private set => SetProperty(ref connectedServerName, value);
    }

    public string ConnectedServerDetail
    {
        get => connectedServerDetail;
        private set => SetProperty(ref connectedServerDetail, value);
    }

    public string CurrentOperatorDisplayName
    {
        get => currentOperatorDisplayName;
        private set => SetProperty(ref currentOperatorDisplayName, value);
    }

    public string CurrentOperatorRolesDisplay
    {
        get => currentOperatorRolesDisplay;
        private set => SetProperty(ref currentOperatorRolesDisplay, value);
    }

    public bool IsAdmin
    {
        get => isAdmin;
        private set => SetProperty(ref isAdmin, value);
    }

    public bool IsOperator
    {
        get => isOperator;
        private set => SetProperty(ref isOperator, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public string BusyMessage
    {
        get => busyMessage;
        private set => SetProperty(ref busyMessage, value);
    }

    public bool HasRecoveryAction => recoveryAction is not null;

    public string RecoveryActionLabel
    {
        get => recoveryActionLabel;
        private set => SetProperty(ref recoveryActionLabel, value);
    }

    public string BetSearchText
    {
        get => betSearchText;
        set
        {
            if (SetProperty(ref betSearchText, value))
            {
                CurrentPage = 1;
                RebuildPagedRecentBets();
            }
        }
    }

    public BetStatusFilterOptionViewModel? SelectedBetStatusFilter
    {
        get => selectedBetStatusFilter;
        set
        {
            if (SetProperty(ref selectedBetStatusFilter, value))
            {
                CurrentPage = 1;
                RebuildPagedRecentBets();
            }
        }
    }

    public BetSortOptionViewModel? SelectedBetSortOption
    {
        get => selectedBetSortOption;
        set
        {
            if (SetProperty(ref selectedBetSortOption, value))
            {
                CurrentPage = 1;
                RebuildPagedRecentBets();
            }
        }
    }

    public int SelectedPageSize
    {
        get => selectedPageSize;
        set
        {
            if (SetProperty(ref selectedPageSize, value))
            {
                CurrentPage = 1;
                RebuildPagedRecentBets();
            }
        }
    }

    public int CurrentPage
    {
        get => currentPage;
        private set
        {
            if (SetProperty(ref currentPage, value))
            {
                RaisePropertyChanged(nameof(TotalPages));
                RaisePropertyChanged(nameof(PaginationSummary));
                PreviousPageCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int TotalPages
    {
        get
        {
            var filteredCount = BuildFilteredAndSortedRecentBets().Count;
            return Math.Max(1, (int)Math.Ceiling(filteredCount / (double)Math.Max(1, SelectedPageSize)));
        }
    }

    public string PaginationSummary
    {
        get
        {
            var totalItems = BuildFilteredAndSortedRecentBets().Count;
            if (totalItems == 0)
            {
                return "Pro zadané podmínky teď nemáme žádné odpovídající sázky.";
            }

            var start = ((CurrentPage - 1) * SelectedPageSize) + 1;
            var end = Math.Min(CurrentPage * SelectedPageSize, totalItems);
            return $"Zobrazeno {start}-{end} z celkem {totalItems} sázek";
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand OpenNewBetEditorCommand { get; }

    public AsyncRelayCommand OpenNewMarketEditorCommand { get; }

    public AsyncRelayCommand OpenCustomerDisplayCommand { get; }

    public AsyncRelayCommand SwitchOperatorCommand { get; }

    public AsyncRelayCommand RecoveryActionCommand { get; }

    public AsyncRelayCommand<BetItemViewModel> DeleteBetCommand { get; }

    public AsyncRelayCommand<BetItemViewModel> StartEditCommand { get; }

    public AsyncRelayCommand<BetItemViewModel> AddBettorToExistingBetCommand { get; }

    public AsyncRelayCommand<MarketItemViewModel> StartEditMarketCommand { get; }

    public AsyncRelayCommand<BetItemViewModel> MarkBetAsWonCommand { get; }

    public AsyncRelayCommand<BetItemViewModel> MarkBetAsLostCommand { get; }

    public AsyncRelayCommand<BetItemViewModel> ResetBetOutcomeCommand { get; }

    public RelayCommand PreviousPageCommand { get; }

    public RelayCommand NextPageCommand { get; }

    public RelayCommand ResetBetFiltersCommand { get; }

    public async Task InitializeAsync()
    {
        ApplySessionContext();
        SetBusyState("Připravujeme váš provozní přehled a načítáme nastavení.");

        try
        {
            var settings = await operationsApiClient.GetSettingsAsync();
            Configuration.Apply(settings);

            await realtimeClient.StartAsync();
            await LoadAsync();
            await RestartAutoRefreshLoopAsync();
        }
        catch (Exception ex)
        {
            RegisterRecoveryForLoadFailure(ex, () => InitializeAsync());
            StatusMessage = GetFriendlyErrorMessage(ex, "Inicializace klienta se nepodařila dokončit.");
        }
        finally
        {
            ClearBusyState();
        }
    }

    public async Task ShutdownAsync()
    {
        await StopAutoRefreshLoopAsync();
        realtimeClient.BetCreated -= HandleBetCreatedAsync;
        await realtimeClient.DisposeAsync();
        loadSemaphore.Dispose();
    }

    private async Task HandleBetCreatedAsync()
    {
        if (!Configuration.EnableRealtimeRefresh)
        {
            ClearRecoveryAction();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusMessage = "Dorazila nová událost, ale živá obnova je momentálně vypnutá.";
            });
            return;
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ClearRecoveryAction();
            StatusMessage = "Právě dorazila nová aktualizace sázek.";
        });

        await LoadAsync(updateStatusMessage: false);
    }

    private async Task LoadAsync(bool updateStatusMessage = true, string? customStatusMessage = null)
    {
        await loadSemaphore.WaitAsync();
        try
        {
            SetBusyState(customStatusMessage ?? "Synchronizujeme data ze serveru D3Bet.");
            var dashboard = await operationsApiClient.GetDashboardAsync();

            Editor.SetBettors(dashboard.Bettors.Select(bettor => new BettorOptionViewModel
            {
                Id = bettor.Id,
                Name = bettor.Name
            }));
            Editor.SetMarkets(dashboard.Markets.Select(market => new MarketOptionViewModel
            {
                Id = market.Id,
                EventName = market.EventName,
                CurrentOdds = market.CurrentOdds,
                IsActive = market.IsActive
            }));

            Markets.Clear();
            foreach (var market in dashboard.Markets)
            {
                Markets.Add(new MarketItemViewModel
                {
                    Id = market.Id,
                    EventName = market.EventName,
                    OpeningOdds = market.OpeningOdds,
                    CurrentOdds = market.CurrentOdds,
                    IsActive = market.IsActive,
                    CreatedAtLocal = market.CreatedAtLocal,
                    OpeningOddsDisplay = market.OpeningOdds.ToString("0.00", CultureInfo.CurrentCulture),
                    CurrentOddsDisplay = market.CurrentOdds.ToString("0.00", CultureInfo.CurrentCulture),
                    CreatedAtDisplay = market.CreatedAtLocal.ToString("g", CultureInfo.CurrentCulture),
                    EditCommand = StartEditMarketCommand
                });
            }

            OpenNewBetEditorCommand.RaiseCanExecuteChanged();

            RecentBets.Clear();
            foreach (var bet in dashboard.RecentBets)
            {
                RecentBets.Add(new BetItemViewModel
                {
                    Id = bet.Id,
                    BettorId = bet.BettorId,
                    BettingMarketId = bet.BettingMarketId,
                    EventName = bet.EventName,
                    BettorName = bet.BettorName,
                    Odds = bet.Odds,
                    Stake = bet.Stake,
                    IsWinning = bet.IsWinning,
                    OutcomeStatus = bet.OutcomeStatus,
                    IsCommissionFeePaid = bet.IsCommissionFeePaid,
                    OddsDisplay = bet.Odds.ToString("0.00", CultureInfo.CurrentCulture),
                    StakeDisplay = bet.Stake.ToString("C", CultureInfo.CurrentCulture),
                    PotentialPayoutDisplay = bet.PotentialPayout.ToString("C", CultureInfo.CurrentCulture),
                    PlacedAtLocal = bet.PlacedAtLocal,
                    PlacedAtDisplay = bet.PlacedAtLocal.ToString("g", CultureInfo.CurrentCulture),
                    EditCommand = StartEditCommand,
                    AddBettorCommand = AddBettorToExistingBetCommand,
                    MarkWonCommand = MarkBetAsWonCommand,
                    MarkLostCommand = MarkBetAsLostCommand,
                    ResetOutcomeCommand = ResetBetOutcomeCommand,
                    DeleteCommand = DeleteBetCommand
                });
            }

            RebuildDashboardAnalytics();
            RebuildPagedRecentBets();
            await RebuildCustomerDisplayFromServerAsync();
            await RebuildAuditFromServerAsync();

            if (updateStatusMessage)
            {
                ClearRecoveryAction();
                StatusMessage = customStatusMessage ?? $"Přehled je aktuální. Načteno {RecentBets.Count} posledních sázek.";
            }

            MarkBetAsWonCommand.RaiseCanExecuteChanged();
            MarkBetAsLostCommand.RaiseCanExecuteChanged();
            ResetBetOutcomeCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            RegisterRecoveryForLoadFailure(ex, () => LoadAsync(updateStatusMessage, customStatusMessage));
            if (updateStatusMessage)
            {
                StatusMessage = GetFriendlyErrorMessage(ex, "Načtení přehledu se tentokrát nepodařilo.");
            }
        }
        finally
        {
            ClearBusyState();
            loadSemaphore.Release();
        }
    }

    private async Task SaveAsync()
    {
        if (!Editor.TryParseValues(out var selectedMarketId, out var parsedStake, out var errorMessage))
        {
            StatusMessage = errorMessage ?? "Neplatná data sázky.";
            return;
        }

        var wasEditing = Editor.IsEditing;

        try
        {
            SetBusyState(wasEditing
                ? "Ukládáme změny na server a přepočítáváme aktuální kurz."
                : "Přijímáme novou sázku a potvrzujeme její uložení.");
            decimal appliedOdds;

            if (Editor.EditingBetId.HasValue)
            {
                appliedOdds = await operationsApiClient.UpdateBetAsync(
                    Editor.EditingBetId.Value,
                    selectedMarketId,
                    Editor.SelectedBettor?.Id,
                    Editor.NewBettorName,
                    parsedStake,
                    Editor.IsCommissionFeePaid);
            }
            else
            {
                appliedOdds = await operationsApiClient.CreateBetAsync(
                    selectedMarketId,
                    Editor.SelectedBettor?.Id,
                    Editor.NewBettorName,
                    parsedStake,
                    Editor.IsCommissionFeePaid);
            }

            Editor.Reset();
            Editor.RequestClose();

            await LoadAsync(updateStatusMessage: false);
            ClearRecoveryAction();
            StatusMessage = wasEditing
                ? $"Změny byly úspěšně uloženy. Aktuální kurz je {appliedOdds:0.00}."
                : $"Nová sázka byla úspěšně přijata. Uložený kurz je {appliedOdds:0.00}.";
        }
        catch (Exception ex)
        {
            RegisterRecoveryAction(ex, wasEditing
                ? () => SaveAsync()
                : () => LoadAsync(updateStatusMessage: true, customStatusMessage: "Obnovujeme přehled po neúspěšném přijetí sázky."), wasEditing ? "Zkusit znovu uložit" : "Obnovit přehled");
            StatusMessage = GetFriendlyErrorMessage(ex, "Sázku se nepodařilo uložit.");
        }
        finally
        {
            ClearBusyState();
        }
    }

    private async Task ChangeOutcomeAsync(BetItemViewModel? bet, BetOutcomeStatus outcomeStatus)
    {
        if (bet is null)
        {
            StatusMessage = "Sázka nebyla vybrána.";
            return;
        }

        var (title, message, successMessage) = outcomeStatus switch
        {
            BetOutcomeStatus.Won => (
                "Potvrdit výhru",
                "Opravdu chcete označit tento tiket jako výherní?",
                "Tiket byl potvrzen jako výherní."),
            BetOutcomeStatus.Lost => (
                "Potvrdit nevýhru",
                "Opravdu chcete označit tento tiket jako nevýherní?",
                "Tiket byl označen jako nevýherní."),
            _ => (
                "Vrátit k vyhodnocení",
                "Opravdu chcete vrátit tento tiket zpět do stavu čeká na vyhodnocení?",
                "Tiket byl vrácen zpět k vyhodnocení.")
        };

        if (!confirmationDialogService.Confirm(title, message))
        {
            StatusMessage = "Změna stavu tiketu byla zrušena.";
            return;
        }

        try
        {
            SetBusyState("Ukládáme nový stav tiketu.");
            await operationsApiClient.SetBetOutcomeAsync(bet.Id, outcomeStatus);
            await LoadAsync(updateStatusMessage: false);
            ClearRecoveryAction();
            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            RegisterRecoveryAction(ex, () => LoadAsync(updateStatusMessage: true, customStatusMessage: "Načítáme aktuální stav tiketu ze serveru."), "Obnovit přehled");
            StatusMessage = GetFriendlyErrorMessage(ex, "Stav tiketu se nepodařilo změnit.");
        }
        finally
        {
            ClearBusyState();
        }
    }

    private async Task DeleteBetAsync(BetItemViewModel? bet)
    {
        if (bet is null)
        {
            StatusMessage = "Sázka nebyla vybrána.";
            return;
        }

        try
        {
            SetBusyState("Odstraňujeme sázku a obnovujeme přehled.");
            await operationsApiClient.DeleteBetAsync(bet.Id);
            if (Editor.EditingBetId == bet.Id)
            {
                Editor.Reset();
            }

            await LoadAsync(updateStatusMessage: false);
            ClearRecoveryAction();
            StatusMessage = "Sázka byla úspěšně odstraněna z přehledu.";
        }
        catch (Exception ex)
        {
            RegisterRecoveryAction(ex, () => LoadAsync(updateStatusMessage: true, customStatusMessage: "Obnovujeme přehled po neúspěšném mazání sázky."), "Obnovit přehled");
            StatusMessage = GetFriendlyErrorMessage(ex, "Sázku se nepodařilo odstranit.");
        }
        finally
        {
            ClearBusyState();
        }
    }

    private Task StartEditAsync(BetItemViewModel? bet)
    {
        if (bet is null)
        {
            StatusMessage = "Sázka nebyla vybrána.";
            return Task.CompletedTask;
        }

        Editor.BeginEdit(bet);
        StatusMessage = "Editor je připravený pro úpravu vybrané sázky.";
        return betEditorWindowService.ShowAsync(Editor);
    }

    private Task AddBettorToExistingBetAsync(BetItemViewModel? bet)
    {
        if (bet is null)
        {
            StatusMessage = "Sázka nebyla vybrána.";
            return Task.CompletedTask;
        }

        Editor.PrepareAdditionalBettor(bet);
        StatusMessage = "Editor je připravený pro přidání dalšího tipéra na stejný tip.";
        return betEditorWindowService.ShowAsync(Editor);
    }

    private Task CancelEditAsync()
    {
        Editor.Reset();
        Editor.RequestClose();
        StatusMessage = "Editor byl zavřený a změny se neuložily.";
        return Task.CompletedTask;
    }

    private Task OpenNewBetEditorAsync()
    {
        if (!Markets.Any(market => market.IsActive))
        {
            StatusMessage = "Nejdřív je potřeba vypsat alespoň jednu aktivní událost.";
            return Task.CompletedTask;
        }

        Editor.BeginCreate();
        StatusMessage = "Editor je připravený pro přijetí sázky na vypsanou událost.";
        return betEditorWindowService.ShowAsync(Editor);
    }

    private Task OpenNewMarketEditorAsync()
    {
        if (!IsAdmin)
        {
            StatusMessage = "Správa vypsaných událostí je dostupná jen pro administrátora.";
            return Task.CompletedTask;
        }

        MarketEditor.BeginCreate();
        StatusMessage = "Editor je připravený pro vypsání nové události.";
        return marketEditorWindowService.ShowAsync(MarketEditor);
    }

    private Task StartEditMarketAsync(MarketItemViewModel? market)
    {
        if (!IsAdmin)
        {
            StatusMessage = "Úpravy vypsaných událostí jsou dostupné jen pro administrátora.";
            return Task.CompletedTask;
        }

        if (market is null)
        {
            StatusMessage = "Událost nebyla vybrána.";
            return Task.CompletedTask;
        }

        MarketEditor.BeginEdit(market);
        StatusMessage = "Editor je připravený pro úpravu vypsané události.";
        return marketEditorWindowService.ShowAsync(MarketEditor);
    }

    private async Task SaveMarketAsync()
    {
        if (!IsAdmin)
        {
            StatusMessage = "Správa vypsaných událostí je dostupná jen pro administrátora.";
            return;
        }

        if (!MarketEditor.TryParse(out var parsedOpeningOdds, out var errorMessage))
        {
            StatusMessage = errorMessage ?? "Neplatná data události.";
            return;
        }

        try
        {
            SetBusyState(MarketEditor.EditingMarketId.HasValue
                ? "Ukládáme změny vypsané události."
                : "Vypisujeme novou událost do nabídky.");
            if (MarketEditor.EditingMarketId.HasValue)
            {
                await operationsApiClient.UpdateMarketAsync(
                    MarketEditor.EditingMarketId.Value,
                    MarketEditor.EventName,
                    parsedOpeningOdds,
                    MarketEditor.IsActive);
                ClearRecoveryAction();
                StatusMessage = "Vypsaná událost byla upravena.";
            }
            else
            {
                await operationsApiClient.CreateMarketAsync(
                    MarketEditor.EventName,
                    parsedOpeningOdds,
                    MarketEditor.IsActive);
                ClearRecoveryAction();
                StatusMessage = "Nová událost byla úspěšně vypsána.";
            }

            MarketEditor.RequestClose();
            await LoadAsync(updateStatusMessage: false);
        }
        catch (Exception ex)
        {
            RegisterRecoveryAction(ex, MarketEditor.EditingMarketId.HasValue
                ? () => SaveMarketAsync()
                : () => LoadAsync(updateStatusMessage: true, customStatusMessage: "Načítáme aktuální přehled vypsaných událostí."), MarketEditor.EditingMarketId.HasValue ? "Zkusit znovu uložit" : "Obnovit přehled");
            StatusMessage = GetFriendlyErrorMessage(ex, "Vypsanou událost se nepodařilo uložit.");
        }
        finally
        {
            ClearBusyState();
        }
    }

    private Task CancelMarketEditAsync()
    {
        MarketEditor.RequestClose();
        StatusMessage = "Editor vypsaných událostí byl zavřen.";
        return Task.CompletedTask;
    }

    private async Task RestartAutoRefreshLoopAsync()
    {
        await StopAutoRefreshLoopAsync();

        if (!Configuration.EnableAutoRefresh)
        {
            return;
        }

        autoRefreshCts = new CancellationTokenSource();
        autoRefreshTask = RunAutoRefreshLoopAsync(
            TimeSpan.FromSeconds(Configuration.SelectedAutoRefreshInterval?.Seconds ?? AppSettings.Default.AutoRefreshIntervalSeconds),
            autoRefreshCts.Token);
    }

    private async Task RunAutoRefreshLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => StatusMessage = "Probíhá automatická synchronizace přehledu sázek.",
                    System.Windows.Threading.DispatcherPriority.Background,
                    cancellationToken);

                await LoadAsync(
                    customStatusMessage: $"Automaticky obnoveno v {DateTime.Now:HH:mm:ss}.",
                    updateStatusMessage: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => StatusMessage = GetFriendlyErrorMessage(ex, "Automatická synchronizace se tentokrát nepodařila."),
                    System.Windows.Threading.DispatcherPriority.Background,
                    cancellationToken);
            }
        }
    }

    private async Task SaveConfigurationAsync()
    {
        if (!IsAdmin)
        {
            StatusMessage = "Nastavení může měnit jen administrátor.";
            return;
        }

        if (!Configuration.TryCreateSettings(out var settings, out var errorMessage))
        {
            StatusMessage = errorMessage ?? "Nastavení provize není platné.";
            return;
        }

        try
        {
            SetBusyState("Ukládáme provozní nastavení na server.");
            await operationsApiClient.SaveSettingsAsync(settings);
            await RestartAutoRefreshLoopAsync();
            RebuildDashboardAnalytics();

            ClearRecoveryAction();
            StatusMessage = settings.EnableAutoRefresh
                ? $"Nastavení bylo uloženo. Automatická obnova běží každých {settings.AutoRefreshIntervalSeconds} sekund."
                : "Nastavení bylo uloženo. Automatická obnova je vypnutá.";
        }
        catch (Exception ex)
        {
            RegisterRecoveryAction(ex, () => SaveConfigurationAsync(), "Zkusit znovu uložit");
            StatusMessage = GetFriendlyErrorMessage(ex, "Nastavení se nepodařilo uložit.");
        }
        finally
        {
            ClearBusyState();
        }
    }

    private void ResetConfigurationToDefaults()
    {
        if (!IsAdmin)
        {
            StatusMessage = "Nastavení může měnit jen administrátor.";
            return;
        }

        Configuration.ApplyDefaults();
        StatusMessage = "Nastavení bylo vráceno na doporučené hodnoty. Pro potvrzení klikněte na Uložit nastavení.";
    }

    private async Task StopAutoRefreshLoopAsync()
    {
        if (autoRefreshCts is null)
        {
            return;
        }

        await autoRefreshCts.CancelAsync();

        if (autoRefreshTask is not null)
        {
            try
            {
                await autoRefreshTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        autoRefreshTask = null;
        autoRefreshCts.Dispose();
        autoRefreshCts = null;
    }

    private void RebuildDashboardAnalytics()
    {
        var culture = CultureInfo.CurrentCulture;
        var recentBetsSnapshot = RecentBets.ToList();

        var totalBets = recentBetsSnapshot.Count;
        var totalStake = recentBetsSnapshot.Sum(bet => bet.Stake);
        var winningBets = recentBetsSnapshot.Count(bet => bet.OutcomeStatus == BetOutcomeStatus.Won);
        var lostBets = recentBetsSnapshot.Count(bet => bet.OutcomeStatus == BetOutcomeStatus.Lost);
        var pendingBets = recentBetsSnapshot.Count(bet => bet.OutcomeStatus == BetOutcomeStatus.Pending);
        var potentialPayout = recentBetsSnapshot.Sum(bet => bet.Stake * bet.Odds);
        var averageOdds = totalBets == 0 ? 0 : recentBetsSnapshot.Average(bet => bet.Odds);
        var uniqueBettors = recentBetsSnapshot.Select(bet => bet.BettorId).Distinct().Count();
        var winningRate = totalBets == 0 ? 0 : (double)winningBets / totalBets;
        var operatorCommission = recentBetsSnapshot.Sum(CalculateOperatorCommission);
        var commissionRate = ParseCommissionRate();
        var flatFeePerBet = ParseFlatFeePerBet();
        var commissionFormulaLabel = Configuration.SelectedCommissionFormula?.Label ?? "Procento z vkladu";

        DashboardKpis.Clear();
        DashboardKpis.Add(new DashboardKpiViewModel
        {
            Title = "Aktivní sázky",
            Value = totalBets.ToString(culture),
            Subtitle = $"{uniqueBettors} aktivních sázejících právě v přehledu",
            AccentBrush = "#F97316"
        });
        DashboardKpis.Add(new DashboardKpiViewModel
        {
            Title = "Objem přijatých vkladů",
            Value = totalStake.ToString("C", culture),
            Subtitle = pendingBets == 0 ? "Všechny tikety jsou vyřízené" : $"{pendingBets} tiketů čeká na vyhodnocení",
            AccentBrush = "#38BDF8"
        });
        DashboardKpis.Add(new DashboardKpiViewModel
        {
            Title = "Potenciál vaší provize",
            Value = operatorCommission.ToString("C", culture),
            Subtitle = Configuration.EnableOperatorCommission
                ? $"{commissionFormulaLabel} při sazbě {commissionRate:0.##} % a poplatku {flatFeePerBet.ToString("C", culture)} za sázku"
                : "Provizní model je aktuálně vypnutý",
            AccentBrush = "#A855F7"
        });
        DashboardKpis.Add(new DashboardKpiViewModel
        {
            Title = "Možný objem výplat",
            Value = potentialPayout.ToString("C", culture),
            Subtitle = $"Průměrný kurz napříč portfoliem je {averageOdds:0.00}",
            AccentBrush = "#22C55E"
        });
        DashboardKpis.Add(new DashboardKpiViewModel
        {
            Title = "Úspěšnost ticketů",
            Value = $"{winningRate:P0}",
            Subtitle = winningBets == 0
                ? (lostBets == 0 ? "Zatím bez vyhodnocených tiketů" : $"{lostBets} tiketů už skončilo bez výhry")
                : $"{winningBets} tiketů už skončilo výhrou",
            AccentBrush = "#FBBF24"
        });

        AnalyticsSummary = totalBets == 0
            ? "Jakmile přijmete první sázky, uvidíte zde živý přehled výkonu a obchodních výsledků."
            : $"Právě sledujete {totalBets} sázek, objem vkladů {totalStake.ToString("C0", culture)}, potenciál výplat {potentialPayout.ToString("C0", culture)} a provizní výkon {operatorCommission.ToString("C0", culture)}.";

        RebuildBarCollection(
            TopBettors,
            recentBetsSnapshot
                .GroupBy(bet => bet.BettorName)
                .OrderByDescending(group => group.Sum(item => item.Stake))
                .Take(5)
                .Select(group => (
                    Label: group.Key,
                    Metric: (double)group.Sum(item => item.Stake),
                    Value: group.Sum(item => item.Stake).ToString("C0", culture),
                    Share: $"{group.Count()} sázek"))
                .ToList());

        RebuildBarCollection(
            EventDistribution,
            recentBetsSnapshot
                .GroupBy(bet => bet.EventName)
                .OrderByDescending(group => group.Count())
                .Take(5)
                .Select(group => (
                    Label: group.Key,
                    Metric: (double)group.Count(),
                    Value: $"{group.Count()}x",
                    Share: group.Sum(item => item.Stake).ToString("C0", culture)))
                .ToList());

        RebuildTrendCollection(recentBetsSnapshot, culture);
    }

    private static void RebuildBarCollection(
        ObservableCollection<DashboardBarItemViewModel> target,
        IReadOnlyCollection<(string Label, double Metric, string Value, string Share)> source)
    {
        target.Clear();

        if (source.Count == 0)
        {
            return;
        }

        var maxMetric = source
            .Select(item => item.Metric)
            .DefaultIfEmpty(0d)
            .Max();

        foreach (var item in source)
        {
            var ratio = maxMetric <= 0 ? 0.2 : Math.Max(item.Metric / maxMetric, 0.2);

            target.Add(new DashboardBarItemViewModel
            {
                Label = item.Label,
                Value = item.Value,
                Share = item.Share,
                BarWidth = 240 * ratio
            });
        }
    }

    private void RebuildTrendCollection(IReadOnlyCollection<BetItemViewModel> source, CultureInfo culture)
    {
        BetVolumeTrend.Clear();

        var groupedTrend = source
            .GroupBy(bet =>
            {
                var placedAt = bet.PlacedAtLocal;
                return new DateTime(placedAt.Year, placedAt.Month, placedAt.Day, placedAt.Hour, 0, 0);
            })
            .OrderBy(group => group.Key)
            .TakeLast(8)
            .Select(group => new
            {
                Label = group.Key.ToString("HH:mm", culture),
                Count = group.Count(),
                Stake = group.Sum(item => item.Stake)
            })
            .ToList();

        if (groupedTrend.Count == 0)
        {
            return;
        }

        var maxCount = groupedTrend.Max(item => item.Count);

        foreach (var point in groupedTrend)
        {
            var ratio = maxCount == 0 ? 0.18 : Math.Max((double)point.Count / maxCount, 0.18);
            BetVolumeTrend.Add(new DashboardTrendPointViewModel
            {
                Label = point.Label,
                Value = $"{point.Count}x",
                ColumnHeight = 120 * ratio,
            });
        }
    }

    private decimal CalculateOperatorCommission(BetItemViewModel bet)
    {
        if (!Configuration.EnableOperatorCommission)
        {
            return 0m;
        }

        var rate = ParseCommissionRate() / 100m;
        var variableCommission = Configuration.SelectedCommissionFormula?.Key switch
        {
            "PercentFromPotentialPayout" => bet.Stake * bet.Odds * rate,
            "PercentFromExpectedMargin" => Math.Max((bet.Stake * bet.Odds) - bet.Stake, 0m) * rate,
            _ => bet.Stake * rate
        };

        var flatFeeComponent = bet.IsCommissionFeePaid ? ParseFlatFeePerBet() : 0m;
        return variableCommission + flatFeeComponent;
    }

    private decimal ParseCommissionRate()
    {
        return decimal.TryParse(Configuration.OperatorCommissionRatePercent, out var rate) && rate >= 0m
            ? rate
            : AppSettings.Default.OperatorCommissionRatePercent;
    }

    private decimal ParseFlatFeePerBet()
    {
        return decimal.TryParse(Configuration.OperatorFlatFeePerBet, out var flatFee) && flatFee >= 0m
            ? flatFee
            : AppSettings.Default.OperatorFlatFeePerBet;
    }

    private async Task RebuildCustomerDisplayFromServerAsync()
    {
        CustomerDisplayTiles.Clear();
        CustomerTickerItems.Clear();
        var source = await operationsApiClient.GetCustomerDisplayAsync();
        var culture = CultureInfo.CurrentCulture;

        var accentPalette = new[]
        {
            "#F97316",
            "#38BDF8",
            "#22C55E",
            "#A855F7",
            "#FBBF24",
            "#FB7185"
        };

        var groupedEvents = source.Markets
            .OrderByDescending(item => item.TotalStake)
            .ThenBy(item => item.EventName)
            .Take(9)
            .ToList();

        for (var index = 0; index < groupedEvents.Count; index++)
        {
            var item = groupedEvents[index];
            CustomerDisplayTiles.Add(new CustomerDisplayTileViewModel
            {
                EventName = item.EventName,
                OddsDisplay = item.CurrentOdds.ToString("0.00", culture),
                TotalStakeDisplay = item.TotalStake.ToString("C0", culture),
                TicketCountDisplay = item.TicketCount == 1
                    ? "1 přijatý tiket"
                    : $"{item.TicketCount} přijatých tiketů",
                AccentBrush = accentPalette[index % accentPalette.Length]
            });
        }

        foreach (var item in groupedEvents.Take(6))
        {
            CustomerTickerItems.Add(new CustomerTickerItemViewModel
            {
                Headline = item.EventName,
                Detail = $"Kurz {item.CurrentOdds.ToString("0.00", culture)} a celkem vsazeno {item.TotalStake.ToString("C0", culture)}.",
                TimestampDisplay = source.GeneratedAtUtc.ToLocalTime().ToString("HH:mm", culture)
            });
        }

        CustomerDisplayUpdatedAt = source.Markets.Count == 0
            ? "Veřejná obrazovka čeká na první přijaté sázky."
            : $"Aktualizováno {source.GeneratedAtUtc.ToLocalTime():HH:mm:ss}";
    }

    private async Task RebuildAuditFromServerAsync()
    {
        AuditEntries.Clear();

        if (!IsAdmin)
        {
            AuditSummary = "Auditní přehled je dostupný jen pro administrátora.";
            return;
        }

        var culture = CultureInfo.CurrentCulture;
        var source = await operationsApiClient.GetAuditLogAsync();
        foreach (var item in source)
        {
            AuditEntries.Add(new AuditLogItemViewModel
            {
                Id = item.Id,
                TimestampDisplay = item.CreatedAtUtc.ToLocalTime().ToString("g", culture),
                ActionDisplay = item.Action,
                EntityDisplay = $"{item.EntityType} • {item.EntityId}",
                ActorDisplay = string.IsNullOrWhiteSpace(item.ActorRoles)
                    ? item.ActorName
                    : $"{item.ActorName} ({item.ActorRoles})",
                TraceIdDisplay = item.TraceId,
                DetailDisplay = string.IsNullOrWhiteSpace(item.DetailJson)
                    ? "Bez doplňujícího detailu."
                    : item.DetailJson
            });
        }

        AuditSummary = AuditEntries.Count == 0
            ? "Zatím není k dispozici žádná auditní událost."
            : $"Načteno {AuditEntries.Count} posledních auditních záznamů pro provozní dohled.";
    }

    private Task OpenCustomerDisplayAsync()
    {
        StatusMessage = "Zákaznická obrazovka je připravená pro kiosk nebo velký displej.";
        return customerDisplayWindowService.ShowAsync(this);
    }

    private async Task SwitchOperatorAsync()
    {
        try
        {
            SetBusyState("Přepínáme účet a navazujeme nové bezpečné spojení se serverem.");
            StatusMessage = "Přepínáme provozovatele a připravujeme nové bezpečné přihlášení.";
            await realtimeClient.StopAsync();

            var session = await operatorAuthService.ForceReauthenticateAsync(CancellationToken.None);
            ApplySessionContext();

            await realtimeClient.StartAsync();
            await LoadAsync(updateStatusMessage: false);
            ClearRecoveryAction();
            StatusMessage = $"Přihlášení bylo obnoveno pro uživatele {session.UserName}.";
        }
        catch (Exception ex)
        {
            RegisterRecoveryAction(ex, () => SwitchOperatorAsync(), "Přihlásit znovu");
            StatusMessage = GetFriendlyErrorMessage(ex, "Přepnutí účtu se nepodařilo dokončit.");
        }
        finally
        {
            ClearBusyState();
        }
    }

    private void SetBusyState(string message)
    {
        BusyMessage = message;
        IsBusy = true;
        RecoveryActionCommand.RaiseCanExecuteChanged();
    }

    private void ClearBusyState()
    {
        BusyMessage = string.Empty;
        IsBusy = false;
        RecoveryActionCommand.RaiseCanExecuteChanged();
    }

    private void RegisterRecoveryForLoadFailure(Exception exception, Func<Task> action)
    {
        RegisterRecoveryAction(exception, action, exception is ApiClientException apiClientException && apiClientException.StatusCode == System.Net.HttpStatusCode.Unauthorized
            ? "Přihlásit znovu"
            : "Zkusit znovu");
    }

    private void RegisterRecoveryAction(Exception exception, Func<Task> action, string defaultLabel)
    {
        if (exception is ApiClientException apiClientException)
        {
            if (apiClientException.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                ClearRecoveryAction();
                return;
            }

            if (apiClientException.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                recoveryAction = () => SwitchOperatorAsync();
                RecoveryActionLabel = "Přihlásit znovu";
                RecoveryActionCommand.RaiseCanExecuteChanged();
                return;
            }

            if (!apiClientException.IsTransient && defaultLabel == "Zkusit znovu")
            {
                ClearRecoveryAction();
                return;
            }
        }

        recoveryAction = action;
        RecoveryActionLabel = defaultLabel;
        RecoveryActionCommand.RaiseCanExecuteChanged();
    }

    private void ClearRecoveryAction()
    {
        recoveryAction = null;
        RecoveryActionLabel = "Zkusit znovu";
        RecoveryActionCommand.RaiseCanExecuteChanged();
    }

    private async Task ExecuteRecoveryActionAsync()
    {
        var action = recoveryAction;
        if (action is null)
        {
            return;
        }

        await action();
    }

    private static string GetFriendlyErrorMessage(Exception exception, string fallbackMessage)
    {
        return exception switch
        {
            ApiClientException apiClientException => apiClientException.UserMessage,
            OperationCanceledException => "Operace byla přerušena dříve, než se stihla dokončit.",
            InvalidOperationException invalidOperationException when !string.IsNullOrWhiteSpace(invalidOperationException.Message) => invalidOperationException.Message,
            _ when !string.IsNullOrWhiteSpace(exception.Message) => exception.Message,
            _ => fallbackMessage
        };
    }

    private void ApplySessionContext()
    {
        CurrentOperatorDisplayName = operatorSessionContext.DisplayName;
        CurrentOperatorRolesDisplay = operatorSessionContext.RolesDisplay;
        ConnectedServerName = serverConnectionContext.ServerName;
        ConnectedServerDetail = $"{serverConnectionContext.MachineName} • {serverConnectionContext.BaseUrl} • {serverConnectionContext.DiscoverySource}";
        IsAdmin = operatorSessionContext.IsAdmin;
        IsOperator = operatorSessionContext.IsOperator;
        OpenNewMarketEditorCommand.RaiseCanExecuteChanged();
        StartEditMarketCommand.RaiseCanExecuteChanged();
    }

    private void RebuildPagedRecentBets()
    {
        var filtered = BuildFilteredAndSortedRecentBets();
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)Math.Max(1, SelectedPageSize)));

        if (CurrentPage > totalPages)
        {
            CurrentPage = totalPages;
        }

        var pagedItems = filtered
            .Skip((CurrentPage - 1) * SelectedPageSize)
            .Take(SelectedPageSize)
            .ToList();

        VisibleRecentBets.Clear();
        foreach (var item in pagedItems)
        {
            VisibleRecentBets.Add(item);
        }

        RaisePropertyChanged(nameof(TotalPages));
        RaisePropertyChanged(nameof(PaginationSummary));
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    private List<BetItemViewModel> BuildFilteredAndSortedRecentBets()
    {
        IEnumerable<BetItemViewModel> query = RecentBets;

        if (!string.IsNullOrWhiteSpace(BetSearchText))
        {
            var search = BetSearchText.Trim();
            query = query.Where(bet =>
                bet.EventName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                bet.BettorName.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }

        query = SelectedBetStatusFilter?.Key switch
        {
            "winning" => query.Where(bet => bet.OutcomeStatus == BetOutcomeStatus.Won),
            "pending" => query.Where(bet => bet.OutcomeStatus == BetOutcomeStatus.Pending),
            _ => query
        };

        query = SelectedBetSortOption?.Key switch
        {
            "placed_asc" => query.OrderBy(bet => bet.PlacedAtLocal),
            "stake_desc" => query.OrderByDescending(bet => bet.Stake),
            "odds_desc" => query.OrderByDescending(bet => bet.Odds),
            "event_asc" => query.OrderBy(bet => bet.EventName).ThenByDescending(bet => bet.PlacedAtLocal),
            _ => query.OrderByDescending(bet => bet.PlacedAtLocal)
        };

        return query.ToList();
    }

    private void GoToPreviousPage()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        RebuildPagedRecentBets();
    }

    private void GoToNextPage()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }

        CurrentPage++;
        RebuildPagedRecentBets();
    }

    private void ResetBetFilters()
    {
        BetSearchText = string.Empty;
        selectedBetStatusFilter = BetStatusFilters.FirstOrDefault();
        RaisePropertyChanged(nameof(SelectedBetStatusFilter));
        selectedBetSortOption = BetSortOptions.FirstOrDefault();
        RaisePropertyChanged(nameof(SelectedBetSortOption));
        SelectedPageSize = PageSizeOptions.FirstOrDefault(pageSize => pageSize == 10);
        CurrentPage = 1;
        RebuildPagedRecentBets();
        StatusMessage = "Filtry byly vráceny na výchozí nastavení.";
    }
}

public sealed record BetStatusFilterOptionViewModel(string Key, string Label)
{
    public override string ToString() => Label;
}

public sealed record BetSortOptionViewModel(string Key, string Label)
{
    public override string ToString() => Label;
}
