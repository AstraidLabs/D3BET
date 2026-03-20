namespace BettingApp.Wpf.ViewModels;

public sealed class StartupViewModel : ObservableObject
{
    private string title = "D3Bet je téměř připraven";
    private string detail = "Spouštíme prostředí pro rychlou správu sázek, živé statistiky a plynulý provoz bez čekání.";
    private int currentStep;
    private int totalSteps = 1;

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    public string Detail
    {
        get => detail;
        set => SetProperty(ref detail, value);
    }

    public int CurrentStep
    {
        get => currentStep;
        set
        {
            if (SetProperty(ref currentStep, value))
            {
                RaisePropertyChanged(nameof(ProgressValue));
                RaisePropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    public int TotalSteps
    {
        get => totalSteps;
        set
        {
            if (SetProperty(ref totalSteps, value))
            {
                RaisePropertyChanged(nameof(ProgressValue));
                RaisePropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    public double ProgressValue => TotalSteps == 0 ? 0 : (double)CurrentStep / TotalSteps * 100;

    public string ProgressLabel => $"Krok {Math.Max(CurrentStep, 1)} z {Math.Max(TotalSteps, 1)}";

    public void Apply(Services.StartupProgress progress)
    {
        Title = progress.Title;
        Detail = progress.Detail;
        CurrentStep = progress.CurrentStep;
        TotalSteps = progress.TotalSteps;
    }
}
