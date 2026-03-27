using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Services;

public sealed class StartupSequenceRunner(
    MainViewModel mainViewModel,
    PlayerMainViewModel playerMainViewModel,
    LicenseService licenseService,
    OperatorAuthService operatorAuthService,
    ServerDiscoveryService serverDiscoveryService,
    OperatorSessionContext operatorSessionContext,
    ShellModeContext shellModeContext)
{
    public async Task RunAsync(
        Func<StartupProgress, Task> reportProgressAsync,
        Func<CancellationToken, Task> startHostAsync,
        CancellationToken cancellationToken)
    {
        await reportProgressAsync(new StartupProgress("Připravujeme klienta", "Načítáme lokální nastavení, připojení k serveru a bezpečný provoz D3Bet klienta.", 1, 5));
        await startHostAsync(cancellationToken);

        await reportProgressAsync(new StartupProgress("Vyhledáváme server", "Hledáme dostupný backend D3Bet v lokální síti a ověřujeme spojení pro přihlášení i synchronizaci.", 2, 5));
        await serverDiscoveryService.DiscoverAndApplyAsync(cancellationToken);

        await reportProgressAsync(new StartupProgress("Ověřujeme licenci", "Kontrolujeme, že je klient bezpečně spárovaný se serverem a může si stáhnout chráněnou konfiguraci.", 3, 5));
        await licenseService.EnsureLicensedAsync(cancellationToken);

        await reportProgressAsync(new StartupProgress("Ověřujeme provozovatele", "Bezpečně přihlašujeme obsluhu přes OAuth a připravujeme oprávnění pro interní práci.", 4, 5));
        await operatorAuthService.EnsureAuthenticatedAsync(cancellationToken);

        await reportProgressAsync(new StartupProgress("Chystáme vaše prostředí", "Načítáme dashboard, nastavení a první přehledy, abyste mohli ihned navázat na provoz.", 5, 5));
        if (operatorSessionContext.HasRole("Customer") && !operatorSessionContext.IsAdmin && !operatorSessionContext.IsOperator)
        {
            shellModeContext.Set(AppShellMode.Player);
            await playerMainViewModel.InitializeAsync();
        }
        else
        {
            shellModeContext.Set(AppShellMode.Operator);
            await mainViewModel.InitializeAsync();
        }
    }
}

public sealed record StartupProgress(string Title, string Detail, int CurrentStep, int TotalSteps);
