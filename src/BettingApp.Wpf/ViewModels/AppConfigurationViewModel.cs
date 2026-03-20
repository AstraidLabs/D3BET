using System.Collections.ObjectModel;
using BettingApp.Wpf.Commands;
using BettingApp.Wpf.Services;

namespace BettingApp.Wpf.ViewModels;

public sealed class AppConfigurationViewModel : ObservableObject
{
    private bool enableAutoRefresh = true;
    private RefreshIntervalOptionViewModel? selectedAutoRefreshInterval;
    private bool enableRealtimeRefresh = true;
    private bool enableTicketAnimations = true;
    private bool enableOperatorCommission = true;
    private CommissionFormulaOptionViewModel? selectedCommissionFormula;
    private string operatorCommissionRatePercent = "5";
    private string operatorFlatFeePerBet = "0";

    public AppConfigurationViewModel(Func<Task> saveAsync, Action resetToDefaults)
    {
        SaveCommand = new AsyncRelayCommand(saveAsync);
        ResetCommand = new RelayCommand(resetToDefaults);

        AutoRefreshIntervals.Add(new RefreshIntervalOptionViewModel(10, "10 s"));
        AutoRefreshIntervals.Add(new RefreshIntervalOptionViewModel(20, "20 s"));
        AutoRefreshIntervals.Add(new RefreshIntervalOptionViewModel(30, "30 s"));
        AutoRefreshIntervals.Add(new RefreshIntervalOptionViewModel(60, "60 s"));

        CommissionFormulas.Add(new CommissionFormulaOptionViewModel("PercentFromStake", "Procento z vkladu"));
        CommissionFormulas.Add(new CommissionFormulaOptionViewModel("PercentFromPotentialPayout", "Procento z potenciální výhry"));
        CommissionFormulas.Add(new CommissionFormulaOptionViewModel("PercentFromExpectedMargin", "Procento z marže"));

        SelectedAutoRefreshInterval = AutoRefreshIntervals.First();
        SelectedCommissionFormula = CommissionFormulas.First();
    }

    public ObservableCollection<RefreshIntervalOptionViewModel> AutoRefreshIntervals { get; } = new();

    public ObservableCollection<CommissionFormulaOptionViewModel> CommissionFormulas { get; } = new();

    public bool EnableAutoRefresh
    {
        get => enableAutoRefresh;
        set => SetProperty(ref enableAutoRefresh, value);
    }

    public RefreshIntervalOptionViewModel? SelectedAutoRefreshInterval
    {
        get => selectedAutoRefreshInterval;
        set => SetProperty(ref selectedAutoRefreshInterval, value);
    }

    public bool EnableRealtimeRefresh
    {
        get => enableRealtimeRefresh;
        set => SetProperty(ref enableRealtimeRefresh, value);
    }

    public bool EnableTicketAnimations
    {
        get => enableTicketAnimations;
        set => SetProperty(ref enableTicketAnimations, value);
    }

    public bool EnableOperatorCommission
    {
        get => enableOperatorCommission;
        set => SetProperty(ref enableOperatorCommission, value);
    }

    public CommissionFormulaOptionViewModel? SelectedCommissionFormula
    {
        get => selectedCommissionFormula;
        set => SetProperty(ref selectedCommissionFormula, value);
    }

    public string OperatorCommissionRatePercent
    {
        get => operatorCommissionRatePercent;
        set => SetProperty(ref operatorCommissionRatePercent, value);
    }

    public string OperatorFlatFeePerBet
    {
        get => operatorFlatFeePerBet;
        set => SetProperty(ref operatorFlatFeePerBet, value);
    }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand ResetCommand { get; }

    public void Apply(AppSettings settings)
    {
        EnableAutoRefresh = settings.EnableAutoRefresh;
        EnableRealtimeRefresh = settings.EnableRealtimeRefresh;
        EnableTicketAnimations = settings.EnableTicketAnimations;
        EnableOperatorCommission = settings.EnableOperatorCommission;
        SelectedAutoRefreshInterval = AutoRefreshIntervals.FirstOrDefault(option => option.Seconds == settings.AutoRefreshIntervalSeconds)
            ?? AutoRefreshIntervals.First();
        SelectedCommissionFormula = CommissionFormulas.FirstOrDefault(option => option.Key == settings.OperatorCommissionFormula)
            ?? CommissionFormulas.First();
        OperatorCommissionRatePercent = settings.OperatorCommissionRatePercent.ToString("0.##");
        OperatorFlatFeePerBet = settings.OperatorFlatFeePerBet.ToString("0.##");
    }

    public void ApplyDefaults() => Apply(AppSettings.Default);

    public bool TryCreateSettings(out AppSettings settings, out string? errorMessage)
    {
        if (!decimal.TryParse(OperatorCommissionRatePercent, out var commissionRate) || commissionRate < 0)
        {
            settings = AppSettings.Default;
            errorMessage = "Provizní sazba musí být nezáporné číslo.";
            return false;
        }

        if (!decimal.TryParse(OperatorFlatFeePerBet, out var flatFeePerBet) || flatFeePerBet < 0)
        {
            settings = AppSettings.Default;
            errorMessage = "Poplatek za sázku musí být nezáporné číslo.";
            return false;
        }

        settings = new AppSettings(
            EnableAutoRefresh,
            SelectedAutoRefreshInterval?.Seconds ?? AppSettings.Default.AutoRefreshIntervalSeconds,
            EnableRealtimeRefresh,
            EnableTicketAnimations,
            EnableOperatorCommission,
            SelectedCommissionFormula?.Key ?? AppSettings.Default.OperatorCommissionFormula,
            commissionRate,
            flatFeePerBet);

        errorMessage = null;
        return true;
    }
}

public sealed record RefreshIntervalOptionViewModel(int Seconds, string Label)
{
    public override string ToString() => Label;
}

public sealed record CommissionFormulaOptionViewModel(string Key, string Label)
{
    public override string ToString() => Label;
}
