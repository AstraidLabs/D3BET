using System.Collections.ObjectModel;
using System.Globalization;
using BettingApp.Domain.Entities;
using BettingApp.Wpf.Commands;
using BettingApp.Wpf.Services;

namespace BettingApp.Wpf.ViewModels;

public sealed class UserAdministrationViewModel : ObservableObject
{
    private readonly OperationsApiClient operationsApiClient;
    private readonly ConfirmationDialogService confirmationDialogService;
    private readonly Func<Task> openEditorAsync;
    private string statusMessage = "Vyberte uživatele ze seznamu nebo založte nový účet.";
    private string searchText = string.Empty;
    private FilterOptionViewModel? selectedRoleFilter;
    private FilterOptionViewModel? selectedSortOption;
    private int selectedPageSize = 20;
    private int currentPage = 1;
    private AdminUserListItemViewModel? selectedUser;
    private bool isBusy;
    private bool isCreatingNewUser;
    private string editedUserId = string.Empty;
    private string userName = string.Empty;
    private string email = string.Empty;
    private bool emailConfirmed = true;
    private bool isBlocked;
    private string accountStatusSummary = "Stav účtu zatím nebyl načten.";
    private string password = string.Empty;
    private string walletSummary = "Kredit zatím nebyl načten.";
    private string walletRateSummary = string.Empty;
    private string creditAdjustmentAmount = "0";
    private string creditAdjustmentMoneyAmount = string.Empty;
    private string creditAdjustmentCurrencyCode = "CZK";
    private string creditAdjustmentReason = string.Empty;
    private string creditAdjustmentReference = string.Empty;
    private string withdrawalDecisionReason = string.Empty;

    public UserAdministrationViewModel(
        OperationsApiClient operationsApiClient,
        ConfirmationDialogService confirmationDialogService,
        Func<Task> openEditorAsync)
    {
        this.operationsApiClient = operationsApiClient;
        this.confirmationDialogService = confirmationDialogService;
        this.openEditorAsync = openEditorAsync;

        SortOptions.Add(new FilterOptionViewModel("user_name_asc", "Jméno A-Z"));
        SortOptions.Add(new FilterOptionViewModel("user_name_desc", "Jméno Z-A"));
        SortOptions.Add(new FilterOptionViewModel("email_asc", "E-mail A-Z"));
        SortOptions.Add(new FilterOptionViewModel("email_desc", "E-mail Z-A"));
        SortOptions.Add(new FilterOptionViewModel("status_desc", "Nejprve aktivní"));

        PageSizeOptions.Add(10);
        PageSizeOptions.Add(20);
        PageSizeOptions.Add(50);
        PageSizeOptions.Add(100);

        selectedSortOption = SortOptions.FirstOrDefault();

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApplyFiltersCommand = new AsyncRelayCommand(RefreshAsync);
        ResetFiltersCommand = new RelayCommand(ResetFilters);
        CreateNewUserCommand = new AsyncRelayCommand(CreateNewUserAsync);
        EditSelectedUserCommand = new AsyncRelayCommand(EditSelectedUserAsync, () => SelectedUser is not null);
        SaveUserCommand = new AsyncRelayCommand(SaveUserAsync, () => !IsBusy);
        DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync, () => SelectedUser is not null && !IsCreatingNewUser);
        ActivateUserCommand = new AsyncRelayCommand(ActivateUserAsync, () => !IsCreatingNewUser && !string.IsNullOrWhiteSpace(editedUserId));
        DeactivateUserCommand = new AsyncRelayCommand(DeactivateUserAsync, () => !IsCreatingNewUser && !string.IsNullOrWhiteSpace(editedUserId));
        BlockUserCommand = new AsyncRelayCommand(BlockUserAsync, () => !IsCreatingNewUser && !string.IsNullOrWhiteSpace(editedUserId));
        UnblockUserCommand = new AsyncRelayCommand(UnblockUserAsync, () => !IsCreatingNewUser && !string.IsNullOrWhiteSpace(editedUserId));
        PreviousPageCommand = new RelayCommand(GoToPreviousPage, () => CurrentPage > 1);
        NextPageCommand = new RelayCommand(GoToNextPage, () => CurrentPage < TotalPages);
        ApplyCreditAdjustmentCommand = new AsyncRelayCommand(ApplyCreditAdjustmentAsync, () => !string.IsNullOrWhiteSpace(editedUserId));
        DeleteBetCommand = new AsyncRelayCommand<UserAdminBetItemViewModel>(DeleteBetAsync, item => item is not null);
        RefundBetCommand = new AsyncRelayCommand<UserAdminBetItemViewModel>(RefundBetAsync, item => item is not null);
        PayoutBetCommand = new AsyncRelayCommand<UserAdminBetItemViewModel>(PayoutBetAsync, item => item is not null);
        ReverseBetPayoutCommand = new AsyncRelayCommand<UserAdminBetItemViewModel>(ReverseBetPayoutAsync, item => item is not null);
        MarkBetAsWonCommand = new AsyncRelayCommand<UserAdminBetItemViewModel>(item => ChangeOutcomeAsync(item, BetOutcomeStatus.Won), item => item is not null);
        MarkBetAsLostCommand = new AsyncRelayCommand<UserAdminBetItemViewModel>(item => ChangeOutcomeAsync(item, BetOutcomeStatus.Lost), item => item is not null);
        ResetBetOutcomeCommand = new AsyncRelayCommand<UserAdminBetItemViewModel>(item => ChangeOutcomeAsync(item, BetOutcomeStatus.Pending), item => item is not null);
        ApproveWithdrawalCommand = new AsyncRelayCommand<UserAdminWithdrawalItemViewModel>(ApproveWithdrawalAsync, item => item is not null);
        RejectWithdrawalCommand = new AsyncRelayCommand<UserAdminWithdrawalItemViewModel>(RejectWithdrawalAsync, item => item is not null);
    }

    public ObservableCollection<AdminUserListItemViewModel> Users { get; } = new();

    public ObservableCollection<SelectableRoleViewModel> AvailableRoles { get; } = new();

    public ObservableCollection<UserAdminBetItemViewModel> Bets { get; } = new();

    public ObservableCollection<UserAdminTransactionItemViewModel> Transactions { get; } = new();

    public ObservableCollection<UserAdminWithdrawalItemViewModel> Withdrawals { get; } = new();

    public ObservableCollection<UserAdminReceiptItemViewModel> Receipts { get; } = new();

    public ObservableCollection<FilterOptionViewModel> RoleFilters { get; } = new();

    public ObservableCollection<FilterOptionViewModel> SortOptions { get; } = new();

    public ObservableCollection<int> PageSizeOptions { get; } = new();

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public string SearchText
    {
        get => searchText;
        set => SetProperty(ref searchText, value);
    }

    public FilterOptionViewModel? SelectedRoleFilter
    {
        get => selectedRoleFilter;
        set => SetProperty(ref selectedRoleFilter, value);
    }

    public FilterOptionViewModel? SelectedSortOption
    {
        get => selectedSortOption;
        set => SetProperty(ref selectedSortOption, value);
    }

    public int SelectedPageSize
    {
        get => selectedPageSize;
        set
        {
            if (SetProperty(ref selectedPageSize, value))
            {
                CurrentPage = 1;
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

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, SelectedPageSize)));

    public int TotalCount { get; private set; }

    public string PaginationSummary => TotalCount == 0
        ? "Zatím nemáme žádné uživatele pro zobrazení."
        : $"Zobrazeno {(CurrentPage - 1) * SelectedPageSize + 1}-{Math.Min(CurrentPage * SelectedPageSize, TotalCount)} z celkem {TotalCount} uživatelů";

    public string EditorTitle => IsCreatingNewUser
        ? "Nový uživatelský účet"
        : string.IsNullOrWhiteSpace(UserName)
            ? "Detail uživatele"
            : $"Profil uživatele {UserName}";

    public string EditorSubtitle => IsCreatingNewUser
        ? "Vyplňte přihlášení, role a výchozí nastavení nového účtu."
        : "Tady spravíte přístup do systému, kredit, sázky i finanční operace vybraného uživatele.";

    public AdminUserListItemViewModel? SelectedUser
    {
        get => selectedUser;
        set
        {
            if (SetProperty(ref selectedUser, value))
            {
                EditSelectedUserCommand.RaiseCanExecuteChanged();
                DeleteUserCommand.RaiseCanExecuteChanged();

            }
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                SaveUserCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCreatingNewUser
    {
        get => isCreatingNewUser;
        private set => SetProperty(ref isCreatingNewUser, value);
    }

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

    public bool EmailConfirmed
    {
        get => emailConfirmed;
        set => SetProperty(ref emailConfirmed, value);
    }

    public bool IsBlocked
    {
        get => isBlocked;
        private set => SetProperty(ref isBlocked, value);
    }

    public string AccountStatusSummary
    {
        get => accountStatusSummary;
        private set => SetProperty(ref accountStatusSummary, value);
    }

    public string Password
    {
        get => password;
        set => SetProperty(ref password, value);
    }

    public string WalletSummary
    {
        get => walletSummary;
        set => SetProperty(ref walletSummary, value);
    }

    public string WalletRateSummary
    {
        get => walletRateSummary;
        set => SetProperty(ref walletRateSummary, value);
    }

    public string CreditAdjustmentAmount
    {
        get => creditAdjustmentAmount;
        set => SetProperty(ref creditAdjustmentAmount, value);
    }

    public string CreditAdjustmentMoneyAmount
    {
        get => creditAdjustmentMoneyAmount;
        set => SetProperty(ref creditAdjustmentMoneyAmount, value);
    }

    public string CreditAdjustmentCurrencyCode
    {
        get => creditAdjustmentCurrencyCode;
        set => SetProperty(ref creditAdjustmentCurrencyCode, value);
    }

    public string CreditAdjustmentReason
    {
        get => creditAdjustmentReason;
        set => SetProperty(ref creditAdjustmentReason, value);
    }

    public string CreditAdjustmentReference
    {
        get => creditAdjustmentReference;
        set => SetProperty(ref creditAdjustmentReference, value);
    }

    public string WithdrawalDecisionReason
    {
        get => withdrawalDecisionReason;
        set => SetProperty(ref withdrawalDecisionReason, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand ApplyFiltersCommand { get; }

    public RelayCommand ResetFiltersCommand { get; }

    public AsyncRelayCommand CreateNewUserCommand { get; }

    public AsyncRelayCommand EditSelectedUserCommand { get; }

    public AsyncRelayCommand SaveUserCommand { get; }

    public AsyncRelayCommand DeleteUserCommand { get; }

    public AsyncRelayCommand ActivateUserCommand { get; }

    public AsyncRelayCommand DeactivateUserCommand { get; }

    public AsyncRelayCommand BlockUserCommand { get; }

    public AsyncRelayCommand UnblockUserCommand { get; }

    public RelayCommand PreviousPageCommand { get; }

    public RelayCommand NextPageCommand { get; }

    public AsyncRelayCommand ApplyCreditAdjustmentCommand { get; }

    public AsyncRelayCommand<UserAdminBetItemViewModel> DeleteBetCommand { get; }

    public AsyncRelayCommand<UserAdminBetItemViewModel> RefundBetCommand { get; }

    public AsyncRelayCommand<UserAdminBetItemViewModel> PayoutBetCommand { get; }

    public AsyncRelayCommand<UserAdminBetItemViewModel> ReverseBetPayoutCommand { get; }

    public AsyncRelayCommand<UserAdminBetItemViewModel> MarkBetAsWonCommand { get; }

    public AsyncRelayCommand<UserAdminBetItemViewModel> MarkBetAsLostCommand { get; }

    public AsyncRelayCommand<UserAdminBetItemViewModel> ResetBetOutcomeCommand { get; }

    public AsyncRelayCommand<UserAdminWithdrawalItemViewModel> ApproveWithdrawalCommand { get; }

    public AsyncRelayCommand<UserAdminWithdrawalItemViewModel> RejectWithdrawalCommand { get; }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            var response = await operationsApiClient.GetAdminUsersAsync(
                SearchText,
                SelectedRoleFilter?.Key == "all" ? null : SelectedRoleFilter?.Key,
                SelectedSortOption?.Key,
                CurrentPage,
                SelectedPageSize);

            TotalCount = response.TotalCount;
            RaisePropertyChanged(nameof(TotalPages));
            RaisePropertyChanged(nameof(PaginationSummary));

            Users.Clear();
            foreach (var item in response.Items)
            {
                Users.Add(new AdminUserListItemViewModel
                {
                    Id = item.Id,
                    UserName = item.UserName,
                    Email = item.Email ?? "Bez e-mailu",
                    RoleDisplay = item.Roles.Length == 0 ? "Bez role" : string.Join(", ", item.Roles),
                    StatusDisplay = item.IsBlocked
                        ? "Blokovaný"
                        : item.EmailConfirmed
                            ? "Aktivní"
                            : "Deaktivovaný",
                    CreditBalanceDisplay = $"{item.CreditBalance:0.00} {item.CreditCode}",
                    BetCountDisplay = item.BetCount.ToString(CultureInfo.CurrentCulture),
                    LastActivityDisplay = item.LastBetPlacedAtUtc.HasValue
                        ? item.LastBetPlacedAtUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                        : "Bez sázek"
                });
            }

            RebuildRoleFilters(response.AvailableRoles);

            if (!IsCreatingNewUser)
            {
                var selectedId = SelectedUser?.Id ?? editedUserId;
                var match = Users.FirstOrDefault(item => item.Id == selectedId);
                if (match is not null)
                {
                    selectedUser = match;
                    RaisePropertyChanged(nameof(SelectedUser));
                }
                else if (Users.Count > 0 && string.IsNullOrWhiteSpace(editedUserId))
                {
                    SelectedUser = Users[0];
                }
            }

            StatusMessage = Users.Count == 0
                ? "Seznam uživatelů je teď prázdný. Můžete založit první účet."
                : "Seznam uživatelů je připravený k administraci.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task EditSelectedUserAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        await LoadUserDetailAsync(SelectedUser.Id);
        await openEditorAsync();
    }

    private async Task CreateNewUserAsync()
    {
        BeginCreateNewUser();
        await openEditorAsync();
    }

    private async Task LoadUserDetailAsync(string userId)
    {
        try
        {
            IsBusy = true;
            var detail = await operationsApiClient.GetAdminUserDetailAsync(userId);
            ApplyUserDetail(detail);
            StatusMessage = $"Načten detail uživatele {detail.UserName}.";
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

    private void ApplyUserDetail(OperationsApiClient.AdminUserDetailResponse detail)
    {
        IsCreatingNewUser = false;
        RaisePropertyChanged(nameof(EditorTitle));
        RaisePropertyChanged(nameof(EditorSubtitle));
        editedUserId = detail.Id;
        UserName = detail.UserName;
        Email = detail.Email ?? string.Empty;
        EmailConfirmed = detail.EmailConfirmed;
        IsBlocked = detail.IsBlocked;
        AccountStatusSummary = detail.IsBlocked
            ? "Účet je zablokovaný a nepřihlásí se, dokud ho znovu nepovolíte."
            : detail.EmailConfirmed
                ? "Účet je aktivní a může se normálně přihlásit."
                : "Účet je deaktivovaný. Pro další přihlášení ho znovu aktivujte.";
        Password = string.Empty;
        CreditAdjustmentCurrencyCode = detail.Wallet.CreditCode == "D3Kredit" ? "CZK" : detail.Wallet.CreditCode;
        WalletSummary = $"{detail.Wallet.Balance:0.00} {detail.Wallet.CreditCode}";
        WalletRateSummary = $"Aktuální přepočet: {detail.Wallet.MoneyToCreditRate:0.0000} nahoru / {detail.Wallet.CreditToMoneyRate:0.0000} zpět";

        RebuildRoleSelections(detail.AvailableRoles, detail.Roles);
        RebuildBets(detail.Bets);
        RebuildTransactions(detail.Transactions);
        RebuildWithdrawals(detail.Withdrawals);
        RebuildReceipts(detail.Receipts);
        ApplyCreditAdjustmentCommand.RaiseCanExecuteChanged();
        DeleteUserCommand.RaiseCanExecuteChanged();
        ActivateUserCommand.RaiseCanExecuteChanged();
        DeactivateUserCommand.RaiseCanExecuteChanged();
        BlockUserCommand.RaiseCanExecuteChanged();
        UnblockUserCommand.RaiseCanExecuteChanged();
    }

    private void BeginCreateNewUser()
    {
        IsCreatingNewUser = true;
        RaisePropertyChanged(nameof(EditorTitle));
        RaisePropertyChanged(nameof(EditorSubtitle));
        editedUserId = string.Empty;
        SelectedUser = null;
        UserName = string.Empty;
        Email = string.Empty;
        EmailConfirmed = true;
        IsBlocked = false;
        AccountStatusSummary = "Nový účet bude po vytvoření připravený podle zvoleného nastavení.";
        Password = string.Empty;
        WalletSummary = "Nový uživatel zatím nemá založený kreditní profil.";
        WalletRateSummary = "Po vytvoření účtu bude možné připsat kredit i spravovat sázky.";
        CreditAdjustmentAmount = "0";
        CreditAdjustmentMoneyAmount = string.Empty;
        CreditAdjustmentReason = string.Empty;
        CreditAdjustmentReference = string.Empty;
        WithdrawalDecisionReason = string.Empty;
        RebuildBets([]);
        RebuildTransactions([]);
        RebuildWithdrawals([]);
        RebuildReceipts([]);

        foreach (var role in AvailableRoles)
        {
            role.IsSelected = role.Name == "Customer";
        }

        StatusMessage = "Vyplňte údaje pro nový účet a uložte ho do systému.";
        DeleteUserCommand.RaiseCanExecuteChanged();
        ApplyCreditAdjustmentCommand.RaiseCanExecuteChanged();
        ActivateUserCommand.RaiseCanExecuteChanged();
        DeactivateUserCommand.RaiseCanExecuteChanged();
        BlockUserCommand.RaiseCanExecuteChanged();
        UnblockUserCommand.RaiseCanExecuteChanged();
    }

    private async Task SaveUserAsync()
    {
        try
        {
            IsBusy = true;
            var selectedRoles = AvailableRoles
                .Where(role => role.IsSelected)
                .Select(role => role.Name)
                .ToArray();

            OperationsApiClient.AdminUserDetailResponse detail;
            if (IsCreatingNewUser)
            {
                if (string.IsNullOrWhiteSpace(Password))
                {
                    throw new InvalidOperationException("Pro nový účet je potřeba zadat heslo.");
                }

                detail = await operationsApiClient.CreateAdminUserAsync(
                    UserName.Trim(),
                    Email.Trim(),
                    EmailConfirmed,
                    selectedRoles,
                    Password.Trim());
            }
            else
            {
                detail = await operationsApiClient.UpdateAdminUserAsync(
                    editedUserId,
                    UserName.Trim(),
                    Email.Trim(),
                    EmailConfirmed,
                    selectedRoles,
                    string.IsNullOrWhiteSpace(Password) ? null : Password.Trim());
            }

            ApplyUserDetail(detail);
            await RefreshAsync();
            SelectedUser = Users.FirstOrDefault(item => item.Id == detail.Id);
            StatusMessage = IsCreatingNewUser
                ? "Nový uživatel byl úspěšně vytvořen."
                : "Změny uživatele byly uloženy.";
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

    private async Task DeleteUserAsync()
    {
        if (SelectedUser is null || IsCreatingNewUser)
        {
            return;
        }

        if (!confirmationDialogService.Confirm("Smazat uživatele", $"Opravdu chcete smazat účet {SelectedUser.UserName}? Tím odstraníte i jeho sázky a kreditní historii."))
        {
            return;
        }

        try
        {
            IsBusy = true;
            await operationsApiClient.DeleteAdminUserAsync(SelectedUser.Id);
            BeginCreateNewUser();
            await RefreshAsync();
            StatusMessage = "Uživatel byl smazán ze systému.";
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

    private async Task ActivateUserAsync()
    {
        if (string.IsNullOrWhiteSpace(editedUserId) || IsCreatingNewUser)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var detail = await operationsApiClient.ActivateAdminUserAsync(editedUserId);
            ApplyUserDetail(detail);
            await RefreshAsync();
            StatusMessage = "Účet byl aktivovaný a znovu se může přihlásit.";
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

    private async Task DeactivateUserAsync()
    {
        if (string.IsNullOrWhiteSpace(editedUserId) || IsCreatingNewUser)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var detail = await operationsApiClient.DeactivateAdminUserAsync(editedUserId);
            ApplyUserDetail(detail);
            await RefreshAsync();
            StatusMessage = "Účet byl deaktivovaný a čeká na znovuaktivaci.";
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

    private async Task BlockUserAsync()
    {
        if (string.IsNullOrWhiteSpace(editedUserId) || IsCreatingNewUser)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var detail = await operationsApiClient.BlockAdminUserAsync(editedUserId);
            ApplyUserDetail(detail);
            await RefreshAsync();
            StatusMessage = "Účet byl zablokovaný. Přihlášení je teď pozastavené.";
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

    private async Task UnblockUserAsync()
    {
        if (string.IsNullOrWhiteSpace(editedUserId) || IsCreatingNewUser)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var detail = await operationsApiClient.UnblockAdminUserAsync(editedUserId);
            ApplyUserDetail(detail);
            await RefreshAsync();
            StatusMessage = "Blokace byla zrušena a účet je znovu dostupný.";
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

    private async Task ApplyCreditAdjustmentAsync()
    {
        if (string.IsNullOrWhiteSpace(editedUserId))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var detail = await operationsApiClient.GetAdminUserDetailAsync(editedUserId);
            if (detail.BettorId is null)
            {
                throw new InvalidOperationException("Vybraný uživatel zatím nemá hráčský profil pro práci s kreditem.");
            }

            var creditAmount = ParseDecimal(CreditAdjustmentAmount, "Změna kreditu musí být platné číslo.");
            var realMoneyAmount = string.IsNullOrWhiteSpace(CreditAdjustmentMoneyAmount)
                ? (decimal?)null
                : ParseDecimal(CreditAdjustmentMoneyAmount, "Částka v měně musí být platné číslo.");
            if (string.IsNullOrWhiteSpace(CreditAdjustmentReason))
            {
                throw new InvalidOperationException("U ruční úpravy kreditu zadejte důvod.");
            }

            await operationsApiClient.ApplyD3CreditManualAdjustmentAsync(
                detail.BettorId,
                detail.UserName,
                creditAmount,
                realMoneyAmount,
                CreditAdjustmentCurrencyCode,
                CreditAdjustmentReason.Trim(),
                CreditAdjustmentReference);

            await LoadUserDetailAsync(editedUserId);
            await RefreshAsync();
            StatusMessage = "Kredit byl upraven podle zadaných pravidel.";
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

    private async Task DeleteBetAsync(UserAdminBetItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (!confirmationDialogService.Confirm("Smazat sázku", $"Opravdu chcete smazat sázku na událost {item.EventName}?"))
        {
            return;
        }

        try
        {
            IsBusy = true;
            await operationsApiClient.DeleteBetAsync(item.Id);
            await LoadUserDetailAsync(editedUserId);
            StatusMessage = "Sázka byla smazána.";
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

    private async Task RefundBetAsync(UserAdminBetItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await operationsApiClient.RefundD3CreditBetAsync(item.Id, "Vrácení kreditu administrátorem v uživatelské správě.");
            await LoadUserDetailAsync(editedUserId);
            StatusMessage = "Kredit za vybranou sázku byl vrácen.";
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

    private async Task PayoutBetAsync(UserAdminBetItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var reason = string.IsNullOrWhiteSpace(WithdrawalDecisionReason)
                ? "Ruční připsání výhry administrátorem v uživatelské správě."
                : WithdrawalDecisionReason.Trim();
            await operationsApiClient.PayoutD3CreditBetAsync(item.Id, reason);
            await LoadUserDetailAsync(editedUserId);
            await RefreshAsync();
            StatusMessage = "Výhra byla připsaná do hráčovy peněženky.";
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

    private async Task ReverseBetPayoutAsync(UserAdminBetItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var reason = string.IsNullOrWhiteSpace(WithdrawalDecisionReason)
                ? "Odebrání dříve připsané výhry administrátorem v uživatelské správě."
                : WithdrawalDecisionReason.Trim();
            await operationsApiClient.ReverseD3CreditBetPayoutAsync(item.Id, reason);
            await LoadUserDetailAsync(editedUserId);
            await RefreshAsync();
            StatusMessage = "Připsaná výhra byla z peněženky odebrána.";
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

    private async Task ChangeOutcomeAsync(UserAdminBetItemViewModel? item, BetOutcomeStatus outcomeStatus)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await operationsApiClient.SetBetOutcomeAsync(item.Id, outcomeStatus);
            await LoadUserDetailAsync(editedUserId);
            StatusMessage = "Stav sázky byl aktualizován.";
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

    private async Task ApproveWithdrawalAsync(UserAdminWithdrawalItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var reason = string.IsNullOrWhiteSpace(WithdrawalDecisionReason)
                ? "Výběr byl schválen administrátorem v uživatelské správě."
                : WithdrawalDecisionReason.Trim();
            await operationsApiClient.ApproveWithdrawalAsync(item.Id, reason);
            await LoadUserDetailAsync(editedUserId);
            await RefreshAsync();
            StatusMessage = "Požadavek na výběr byl schválen.";
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

    private async Task RejectWithdrawalAsync(UserAdminWithdrawalItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var reason = string.IsNullOrWhiteSpace(WithdrawalDecisionReason)
                ? "Výběr byl zamítnut administrátorem a kredit vrácen zpět."
                : WithdrawalDecisionReason.Trim();
            await operationsApiClient.RejectWithdrawalAsync(item.Id, reason);
            await LoadUserDetailAsync(editedUserId);
            await RefreshAsync();
            StatusMessage = "Požadavek na výběr byl zamítnut a kredit vrácen.";
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

    private void GoToPreviousPage()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        _ = RefreshAsync();
    }

    private void GoToNextPage()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }

        CurrentPage++;
        _ = RefreshAsync();
    }

    private void ResetFilters()
    {
        SearchText = string.Empty;
        SelectedRoleFilter = RoleFilters.FirstOrDefault();
        SelectedSortOption = SortOptions.FirstOrDefault();
        SelectedPageSize = 20;
        CurrentPage = 1;
        _ = RefreshAsync();
    }

    private void RebuildRoleFilters(IEnumerable<string> roles)
    {
        var selectedRoleKey = SelectedRoleFilter?.Key ?? "all";
        RoleFilters.Clear();
        RoleFilters.Add(new FilterOptionViewModel("all", "Všechny role"));
        foreach (var role in roles.OrderBy(role => role))
        {
            RoleFilters.Add(new FilterOptionViewModel(role, role));
        }

        SelectedRoleFilter = RoleFilters.FirstOrDefault(role => role.Key == selectedRoleKey) ?? RoleFilters.First();
    }

    private void RebuildRoleSelections(IEnumerable<string> availableRoles, IEnumerable<string> selectedRoles)
    {
        var selectedLookup = selectedRoles.ToHashSet(StringComparer.Ordinal);
        AvailableRoles.Clear();
        foreach (var role in availableRoles.OrderBy(role => role))
        {
            AvailableRoles.Add(new SelectableRoleViewModel(role, selectedLookup.Contains(role)));
        }
    }

    private void RebuildBets(IEnumerable<OperationsApiClient.AdminUserBetResponse> bets)
    {
        Bets.Clear();
        foreach (var bet in bets)
        {
            Bets.Add(new UserAdminBetItemViewModel
            {
                Id = bet.Id,
                EventName = bet.EventName,
                OutcomeStatus = bet.OutcomeStatus,
                OutcomeDisplay = bet.OutcomeStatus switch
                {
                    BetOutcomeStatus.Won => "Výherní",
                    BetOutcomeStatus.Lost => "Nevýherní",
                    _ => "Čeká na vyhodnocení"
                },
                StakeDisplay = string.Equals(bet.StakeCurrencyCode, "D3Kredit", StringComparison.OrdinalIgnoreCase)
                    ? $"{bet.Stake:0.00} {bet.StakeCurrencyCode}"
                    : bet.StakeRealMoneyEquivalent.ToString("C", CultureInfo.CurrentCulture),
                PotentialPayoutDisplay = string.Equals(bet.StakeCurrencyCode, "D3Kredit", StringComparison.OrdinalIgnoreCase)
                    ? $"{bet.PotentialPayout:0.00} {bet.StakeCurrencyCode}"
                    : bet.PotentialPayout.ToString("C", CultureInfo.CurrentCulture),
                PlacedAtDisplay = bet.PlacedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                IsPayoutProcessed = bet.IsPayoutProcessed,
                PayoutStatusDisplay = bet.IsPayoutProcessed
                    ? "Výhra už byla připsaná"
                    : bet.OutcomeStatus == BetOutcomeStatus.Won
                        ? "Výhra čeká na připsání"
                        : "Bez vyplacení"
            });
        }
    }

    private void RebuildTransactions(IEnumerable<OperationsApiClient.AdminUserCreditTransactionResponse> transactions)
    {
        Transactions.Clear();
        foreach (var transaction in transactions)
        {
            Transactions.Add(new UserAdminTransactionItemViewModel
            {
                TypeDisplay = transaction.Type.ToString(),
                AmountDisplay = $"{transaction.CreditAmount:0.00} D3Kredit",
                MoneyDisplay = $"{transaction.RealMoneyAmount:0.00} {transaction.RealCurrencyCode}",
                Description = transaction.Description,
                Reference = transaction.Reference,
                CreatedAtDisplay = transaction.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            });
        }
    }

    private void RebuildWithdrawals(IEnumerable<OperationsApiClient.CreditWithdrawalResponse> withdrawals)
    {
        Withdrawals.Clear();
        foreach (var withdrawal in withdrawals)
        {
            Withdrawals.Add(new UserAdminWithdrawalItemViewModel
            {
                Id = withdrawal.Id,
                Status = withdrawal.Status,
                RequestedAtDisplay = withdrawal.RequestedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                ProcessedAtDisplay = withdrawal.ProcessedAtUtc?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "Čeká na zpracování",
                AmountDisplay = $"{withdrawal.CreditAmount:0.00} D3Kredit",
                MoneyDisplay = $"{withdrawal.RealMoneyAmount:0.00} {withdrawal.RealCurrencyCode}",
                StatusDisplay = withdrawal.Status switch
                {
                    CreditWithdrawalRequestStatus.Paid => "Vyplaceno",
                    CreditWithdrawalRequestStatus.Rejected => "Zamítnuto",
                    _ => "Čeká na rozhodnutí"
                },
                Reason = withdrawal.Reason,
                ProcessedReason = string.IsNullOrWhiteSpace(withdrawal.ProcessedReason) ? "Bez poznámky" : withdrawal.ProcessedReason,
                Reference = withdrawal.Reference,
                IsPending = withdrawal.Status == CreditWithdrawalRequestStatus.Pending
            });
        }
    }

    private void RebuildReceipts(IEnumerable<OperationsApiClient.ElectronicReceiptResponse> receipts)
    {
        Receipts.Clear();
        foreach (var receipt in receipts)
        {
            Receipts.Add(new UserAdminReceiptItemViewModel
            {
                DocumentNumber = receipt.DocumentNumber,
                Title = receipt.Title,
                AmountDisplay = $"{receipt.CreditAmount:0.00} D3Kredit",
                MoneyDisplay = $"{receipt.RealMoneyAmount:0.00} {receipt.RealCurrencyCode}",
                Summary = receipt.Summary,
                IssuedAtDisplay = receipt.IssuedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            });
        }
    }

    private static decimal ParseDecimal(string value, string errorMessage)
    {
        if (decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(errorMessage);
    }
}

public sealed class AdminUserListItemViewModel : ObservableObject
{
    public string Id { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RoleDisplay { get; init; } = string.Empty;

    public string StatusDisplay { get; init; } = string.Empty;

    public string CreditBalanceDisplay { get; init; } = string.Empty;

    public string BetCountDisplay { get; init; } = string.Empty;

    public string LastActivityDisplay { get; init; } = string.Empty;
}

public sealed class UserAdminBetItemViewModel : ObservableObject
{
    public Guid Id { get; init; }

    public string EventName { get; init; } = string.Empty;

    public string OutcomeDisplay { get; init; } = string.Empty;

    public BetOutcomeStatus OutcomeStatus { get; init; }

    public string StakeDisplay { get; init; } = string.Empty;

    public string PotentialPayoutDisplay { get; init; } = string.Empty;

    public string PlacedAtDisplay { get; init; } = string.Empty;

    public bool IsPayoutProcessed { get; init; }

    public string PayoutStatusDisplay { get; init; } = string.Empty;
}

public sealed class UserAdminTransactionItemViewModel : ObservableObject
{
    public string TypeDisplay { get; init; } = string.Empty;

    public string AmountDisplay { get; init; } = string.Empty;

    public string MoneyDisplay { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public string CreatedAtDisplay { get; init; } = string.Empty;
}

public sealed class UserAdminWithdrawalItemViewModel : ObservableObject
{
    public Guid Id { get; init; }

    public CreditWithdrawalRequestStatus Status { get; init; }

    public bool IsPending { get; init; }

    public string RequestedAtDisplay { get; init; } = string.Empty;

    public string ProcessedAtDisplay { get; init; } = string.Empty;

    public string AmountDisplay { get; init; } = string.Empty;

    public string MoneyDisplay { get; init; } = string.Empty;

    public string StatusDisplay { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string ProcessedReason { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;
}

public sealed class UserAdminReceiptItemViewModel : ObservableObject
{
    public string DocumentNumber { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string AmountDisplay { get; init; } = string.Empty;

    public string MoneyDisplay { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string IssuedAtDisplay { get; init; } = string.Empty;
}

public sealed class SelectableRoleViewModel : ObservableObject
{
    private bool isSelected;

    public SelectableRoleViewModel(string name, bool isSelected)
    {
        Name = name;
        this.isSelected = isSelected;
    }

    public string Name { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}

public sealed record FilterOptionViewModel(string Key, string Label)
{
    public override string ToString() => Label;
}
