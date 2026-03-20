using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class StartupWindow : Window
{
    public StartupWindow(StartupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public Task PlayExitTransitionAsync()
    {
        var completionSource = new TaskCompletionSource();

        var scaleTransform = RenderTransform as ScaleTransform;
        if (scaleTransform is null)
        {
            scaleTransform = new ScaleTransform(1, 1);
            RenderTransform = scaleTransform;
        }

        RenderTransformOrigin = new Point(0.5, 0.5);

        var storyboard = new Storyboard();

        var fadeAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        var scaleXAnimation = new DoubleAnimation
        {
            To = 0.975,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        var scaleYAnimation = new DoubleAnimation
        {
            To = 0.975,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        Storyboard.SetTarget(fadeAnimation, this);
        Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(Window.OpacityProperty));

        Storyboard.SetTarget(scaleXAnimation, this);
        Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

        Storyboard.SetTarget(scaleYAnimation, this);
        Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

        storyboard.Children.Add(fadeAnimation);
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Completed += (_, _) => completionSource.TrySetResult();
        storyboard.Begin();

        return completionSource.Task;
    }
}
