using System.Windows;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class MarketEditorWindow : Window
{
    public MarketEditorWindow(MarketEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
