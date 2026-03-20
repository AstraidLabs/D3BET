using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Services;

public sealed class StartupSequenceRunner(
    MainViewModel mainViewModel,
    OperatorAuthService operatorAuthService,
    ServerDiscoveryService serverDiscoveryService)
{
    public async Task RunAsync(
        Func<StartupProgress, Task> reportProgressAsync,
        Func<CancellationToken, Task> startHostAsync,
        CancellationToken cancellationToken)
    {
        await reportProgressAsync(new StartupProgress("Připravujeme klienta", "Načítáme lokální nastavení, připojení k serveru a bezpečný provoz D3Bet klienta.", 1, 4));
        await startHostAsync(cancellationToken);

        await reportProgressAsync(new StartupProgress("Vyhledáváme server", "Hledáme dostupný backend D3Bet v lokální síti a ověřujeme spojení pro přihlášení i synchronizaci.", 2, 4));
        await serverDiscoveryService.DiscoverAndApplyAsync(cancellationToken);

        await reportProgressAsync(new StartupProgress("Ověřujeme provozovatele", "Bezpečně přihlašujeme obsluhu přes OAuth a připravujeme oprávnění pro interní práci.", 3, 4));
        await operatorAuthService.EnsureAuthenticatedAsync(cancellationToken);

        await reportProgressAsync(new StartupProgress("Chystáme vaše prostředí", "Načítáme dashboard, nastavení a první přehledy, abyste mohli ihned navázat na provoz.", 4, 4));
        await mainViewModel.InitializeAsync();
    }
}

public sealed record StartupProgress(string Title, string Detail, int CurrentStep, int TotalSteps);
