using System.Windows;
using System.Windows.Input;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class CustomerDisplayWindow : Window
{
    public CustomerDisplayWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RefreshButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.RefreshCommand.CanExecute(null))
        {
            viewModel.RefreshCommand.Execute(null);
        }
    }

    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
