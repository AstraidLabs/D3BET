using System.Windows;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class BetEditorWindow : Window
{
    public BetEditorWindow(BetEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
