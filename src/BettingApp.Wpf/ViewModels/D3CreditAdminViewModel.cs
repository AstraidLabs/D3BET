using System.Collections.ObjectModel;
using BettingApp.Wpf.Commands;

namespace BettingApp.Wpf.ViewModels;

public sealed class D3CreditAdminViewModel : ObservableObject
{
    private string creditCode = "D3Kredit";
    private string baseCurrencyCode = "CZK";
    private string baseCreditsPerCurrencyUnit = "10";
    private string baseCurrencyUnitsPerCredit = "0.10";
    private string lowParticipationThreshold = "3";
    private string lowParticipationBoostPercent = "20";
    private string highParticipationThreshold = "10";
    private string highParticipationReductionPercent = "15";
    private string totalStakePressureDivisor = "500";
    private string maxPressureReductionPercent = "20";
    private string oddsVolatilityWeightPercent = "5";
    private string defaultTopUpAmount = "500";
    private bool enableTestTopUpGateway = true;
    private bool enableManualCreditAdjustments = true;
    private bool enableManualBetRefunds = true;
    private string walletSearchText = string.Empty;
    private D3CreditAdminWalletItemViewModel? selectedWallet;
    private string manualCreditAmount = "0";
    private string manualRealMoneyAmount = string.Empty;
    private string manualCurrencyCode = "CZK";
    private string manualReason = string.Empty;
    private string manualReference = string.Empty;
    private string refundBetId = string.Empty;
    private string refundReason = "Ruční vrácení kreditu administrátorem.";
    private D3CreditMarketRuleItemViewModel? selectedMarketRule;

    public D3CreditAdminViewModel(
        Func<Task> refreshAsync,
        Func<Task> saveSettingsAsync,
        Func<Task> applyAdjustmentAsync,
        Func<Task> refundBetAsync,
        Action addMarketRuleAction,
        Action removeSelectedMarketRuleAction)
    {
        RefreshCommand = new AsyncRelayCommand(refreshAsync);
        SaveSettingsCommand = new AsyncRelayCommand(saveSettingsAsync);
        ApplyAdjustmentCommand = new AsyncRelayCommand(applyAdjustmentAsync);
        RefundBetCommand = new AsyncRelayCommand(refundBetAsync);
        AddMarketRuleCommand = new RelayCommand(addMarketRuleAction);
        RemoveSelectedMarketRuleCommand = new RelayCommand(removeSelectedMarketRuleAction, () => SelectedMarketRule is not null);
    }

    public ObservableCollection<D3CreditAdminWalletItemViewModel> Wallets { get; } = new();

    public ObservableCollection<D3CreditAdminTransactionItemViewModel> Transactions { get; } = new();

    public ObservableCollection<D3CreditMarketRuleItemViewModel> MarketRules { get; } = new();

    public string CreditCode
    {
        get => creditCode;
        set => SetProperty(ref creditCode, value);
    }

    public string BaseCurrencyCode
    {
        get => baseCurrencyCode;
        set => SetProperty(ref baseCurrencyCode, value);
    }

    public string BaseCreditsPerCurrencyUnit
    {
        get => baseCreditsPerCurrencyUnit;
        set => SetProperty(ref baseCreditsPerCurrencyUnit, value);
    }

    public string BaseCurrencyUnitsPerCredit
    {
        get => baseCurrencyUnitsPerCredit;
        set => SetProperty(ref baseCurrencyUnitsPerCredit, value);
    }

    public string LowParticipationThreshold
    {
        get => lowParticipationThreshold;
        set => SetProperty(ref lowParticipationThreshold, value);
    }

    public string LowParticipationBoostPercent
    {
        get => lowParticipationBoostPercent;
        set => SetProperty(ref lowParticipationBoostPercent, value);
    }

    public string HighParticipationThreshold
    {
        get => highParticipationThreshold;
        set => SetProperty(ref highParticipationThreshold, value);
    }

    public string HighParticipationReductionPercent
    {
        get => highParticipationReductionPercent;
        set => SetProperty(ref highParticipationReductionPercent, value);
    }

    public string TotalStakePressureDivisor
    {
        get => totalStakePressureDivisor;
        set => SetProperty(ref totalStakePressureDivisor, value);
    }

    public string MaxPressureReductionPercent
    {
        get => maxPressureReductionPercent;
        set => SetProperty(ref maxPressureReductionPercent, value);
    }

    public string OddsVolatilityWeightPercent
    {
        get => oddsVolatilityWeightPercent;
        set => SetProperty(ref oddsVolatilityWeightPercent, value);
    }

    public string DefaultTopUpAmount
    {
        get => defaultTopUpAmount;
        set => SetProperty(ref defaultTopUpAmount, value);
    }

    public bool EnableTestTopUpGateway
    {
        get => enableTestTopUpGateway;
        set => SetProperty(ref enableTestTopUpGateway, value);
    }

    public bool EnableManualCreditAdjustments
    {
        get => enableManualCreditAdjustments;
        set => SetProperty(ref enableManualCreditAdjustments, value);
    }

    public bool EnableManualBetRefunds
    {
        get => enableManualBetRefunds;
        set => SetProperty(ref enableManualBetRefunds, value);
    }

    public string WalletSearchText
    {
        get => walletSearchText;
        set => SetProperty(ref walletSearchText, value);
    }

    public D3CreditAdminWalletItemViewModel? SelectedWallet
    {
        get => selectedWallet;
        set => SetProperty(ref selectedWallet, value);
    }

    public string ManualCreditAmount
    {
        get => manualCreditAmount;
        set => SetProperty(ref manualCreditAmount, value);
    }

    public string ManualRealMoneyAmount
    {
        get => manualRealMoneyAmount;
        set => SetProperty(ref manualRealMoneyAmount, value);
    }

    public string ManualCurrencyCode
    {
        get => manualCurrencyCode;
        set => SetProperty(ref manualCurrencyCode, value);
    }

    public string ManualReason
    {
        get => manualReason;
        set => SetProperty(ref manualReason, value);
    }

    public string ManualReference
    {
        get => manualReference;
        set => SetProperty(ref manualReference, value);
    }

    public string RefundBetId
    {
        get => refundBetId;
        set => SetProperty(ref refundBetId, value);
    }

    public string RefundReason
    {
        get => refundReason;
        set => SetProperty(ref refundReason, value);
    }

    public D3CreditMarketRuleItemViewModel? SelectedMarketRule
    {
        get => selectedMarketRule;
        set
        {
            if (SetProperty(ref selectedMarketRule, value))
            {
                RemoveSelectedMarketRuleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand SaveSettingsCommand { get; }

    public AsyncRelayCommand ApplyAdjustmentCommand { get; }

    public AsyncRelayCommand RefundBetCommand { get; }

    public RelayCommand AddMarketRuleCommand { get; }

    public RelayCommand RemoveSelectedMarketRuleCommand { get; }
}

public sealed class D3CreditAdminWalletItemViewModel
{
    public Guid BettorId { get; init; }

    public string BettorName { get; init; } = string.Empty;

    public string BalanceDisplay { get; init; } = string.Empty;

    public string RatesDisplay { get; init; } = string.Empty;

    public string UpdatedAtDisplay { get; init; } = string.Empty;
}

public sealed class D3CreditAdminTransactionItemViewModel
{
    public string TimestampDisplay { get; init; } = string.Empty;

    public string BettorName { get; init; } = string.Empty;

    public string TypeDisplay { get; init; } = string.Empty;

    public string CreditDisplay { get; init; } = string.Empty;

    public string MoneyDisplay { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;
}

public sealed class D3CreditMarketRuleItemViewModel : ObservableObject
{
    private Guid marketId;
    private string marketName = string.Empty;
    private bool isEnabled = true;
    private string additionalMultiplierPercent = "0";
    private string overrideMoneyToCreditRate = string.Empty;
    private string overrideCreditToMoneyRate = string.Empty;
    private string note = string.Empty;

    public Guid MarketId
    {
        get => marketId;
        set => SetProperty(ref marketId, value);
    }

    public string MarketName
    {
        get => marketName;
        set => SetProperty(ref marketName, value);
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }

    public string AdditionalMultiplierPercent
    {
        get => additionalMultiplierPercent;
        set => SetProperty(ref additionalMultiplierPercent, value);
    }

    public string OverrideMoneyToCreditRate
    {
        get => overrideMoneyToCreditRate;
        set => SetProperty(ref overrideMoneyToCreditRate, value);
    }

    public string OverrideCreditToMoneyRate
    {
        get => overrideCreditToMoneyRate;
        set => SetProperty(ref overrideCreditToMoneyRate, value);
    }

    public string Note
    {
        get => note;
        set => SetProperty(ref note, value);
    }

    public override string ToString() => string.IsNullOrWhiteSpace(MarketName) ? MarketId.ToString() : MarketName;
}
