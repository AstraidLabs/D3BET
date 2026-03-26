using System.Windows;
using BettingApp.Wpf.ViewModels;
using BettingApp.Wpf.Views;

namespace BettingApp.Wpf.Services;

public sealed class ProfileWindowService(
    SelfServiceApiClient selfServiceApiClient,
    OperatorAuthService operatorAuthService)
{
    public async Task<bool> ShowAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await operatorAuthService.GetAccessTokenAsync(cancellationToken);
        var viewModel = new ProfileViewModel(selfServiceApiClient, accessToken);
        await viewModel.LoadAsync(cancellationToken);

        var window = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new ProfileWindow(viewModel);
            var owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
            if (owner is not null)
            {
                dialog.Owner = owner;
            }

            return dialog;
        });

        var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(window.ShowDialog);
        if (result == true && viewModel.WasSaved)
        {
            await operatorAuthService.RefreshCurrentSessionAsync(cancellationToken);
            return true;
        }

        return false;
    }
}
