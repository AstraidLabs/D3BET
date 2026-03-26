using System.Windows;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class ProfileWindow : Window
{
    public ProfileWindow(ProfileViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public ProfileViewModel ViewModel { get; }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ViewModel.SaveAsync())
        {
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
