using System.IO;
using System.Windows;
using BettingApp.Wpf.Services;
using BettingApp.Wpf.ViewModels;
using BettingApp.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BettingApp.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? host;
    private IAsyncViewModel? mainViewModel;
    private StartupWindow? startupWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BettingApp");

        Directory.CreateDirectory(appDataDirectory);
        var operatorSessionPath = Path.Combine(appDataDirectory, "operator-session.json");

        host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(new OperatorSessionStore(operatorSessionPath));
                services.AddSingleton(new OperatorSessionContext());
                services.AddSingleton(new ServerConnectionContext());
                services.AddSingleton(new ShellModeContext());
                services.AddSingleton(new OperatorAuthOptions());
                services.AddSingleton<SelfServiceApiClient>();
                services.AddSingleton<ServerDiscoveryService>();
                services.AddSingleton<OperatorAuthService>();
                services.AddSingleton<OperationsApiClient>();
                services.AddSingleton<PlayerApiClient>();
                services.AddSingleton<ProfileWindowService>();
                services.AddSingleton<StartupViewModel>();
                services.AddSingleton<StartupSequenceRunner>();
                services.AddSingleton<BettingRealtimeClient>();
                services.AddSingleton<BetEditorWindowService>();
                services.AddSingleton<CustomerDisplayWindowService>();
                services.AddSingleton<MarketEditorWindowService>();
                services.AddSingleton<ConfirmationDialogService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<PlayerMainViewModel>();
                services.AddSingleton<StartupWindow>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<PlayerWindow>();
            })
            .Build();

        startupWindow = host.Services.GetRequiredService<StartupWindow>();
        startupWindow.Show();

        var startupViewModel = host.Services.GetRequiredService<StartupViewModel>();
        var startupSequenceRunner = host.Services.GetRequiredService<StartupSequenceRunner>();
        var shellModeContext = host.Services.GetRequiredService<ShellModeContext>();

        try
        {
            await startupSequenceRunner.RunAsync(
                async progress =>
                {
                    await Dispatcher.InvokeAsync(() => startupViewModel.Apply(progress));
                },
                cancellationToken => host.StartAsync(cancellationToken),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            startupWindow.Close();
            startupWindow = null;

            if (host is not null)
            {
                await host.StopAsync();
                host.Dispose();
                host = null;
            }

            MessageBox.Show(
                $"Aplikaci se nepodarilo spustit.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Startup chyba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
            return;
        }

        if (shellModeContext.CurrentMode == AppShellMode.Player)
        {
            mainViewModel = host.Services.GetRequiredService<PlayerMainViewModel>();
            var playerWindow = host.Services.GetRequiredService<PlayerWindow>();
            playerWindow.DataContext = mainViewModel;
            MainWindow = playerWindow;
            playerWindow.Show();
        }
        else
        {
            mainViewModel = host.Services.GetRequiredService<MainViewModel>();
            var mainWindow = host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
            await mainWindow.PlayEntranceTransitionAsync();
        }

        if (startupWindow is not null)
        {
            await startupWindow.PlayExitTransitionAsync();
            startupWindow.Close();
        }

        startupWindow = null;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        startupWindow?.Close();

        if (mainViewModel is not null)
        {
            await mainViewModel.ShutdownAsync();
        }

        if (host is not null)
        {
            await host.StopAsync();
            host.Dispose();
        }

        base.OnExit(e);
    }
}
