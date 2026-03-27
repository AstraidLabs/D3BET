using System.Windows;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class UserAdministrationWindow : Window
{
    public UserAdministrationWindow(UserAdministrationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
