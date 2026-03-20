using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Views;

public partial class MainWindow : Window
{
    private bool allowClose;
    private bool isExitTransitionRunning;
    private MainViewModel? subscribedViewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        RootContent.RenderTransform = new TranslateTransform(0, 18);
        DataContextChanged += OnDataContextChanged;
        SubscribeToViewModel(viewModel);
    }

    public Task PlayEntranceTransitionAsync()
    {
        var completionSource = new TaskCompletionSource();

        if (RootContent.RenderTransform is not TranslateTransform translateTransform)
        {
            translateTransform = new TranslateTransform(0, 18);
            RootContent.RenderTransform = translateTransform;
        }

        var storyboard = new Storyboard();

        var fadeAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var slideAnimation = new DoubleAnimation
        {
            From = 18,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(360),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(fadeAnimation, this);
        Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(Window.OpacityProperty));

        Storyboard.SetTarget(slideAnimation, RootContent);
        Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

        storyboard.Children.Add(fadeAnimation);
        storyboard.Children.Add(slideAnimation);
        storyboard.Completed += (_, _) => completionSource.TrySetResult();
        storyboard.Begin();

        return completionSource.Task;
    }

    public Task PlayExitTransitionAsync()
    {
        var completionSource = new TaskCompletionSource();

        if (RootContent.RenderTransform is not TranslateTransform translateTransform)
        {
            translateTransform = new TranslateTransform();
            RootContent.RenderTransform = translateTransform;
        }

        var storyboard = new Storyboard();

        var fadeAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        var slideAnimation = new DoubleAnimation
        {
            To = 16,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        Storyboard.SetTarget(fadeAnimation, this);
        Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(Window.OpacityProperty));

        Storyboard.SetTarget(slideAnimation, RootContent);
        Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

        storyboard.Children.Add(fadeAnimation);
        storyboard.Children.Add(slideAnimation);
        storyboard.Completed += (_, _) => completionSource.TrySetResult();
        storyboard.Begin();

        return completionSource.Task;
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (allowClose || isExitTransitionRunning)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        isExitTransitionRunning = true;

        try
        {
            await PlayExitTransitionAsync();
            allowClose = true;
            Close();
        }
        finally
        {
            isExitTransitionRunning = false;
        }
    }

    private void RecentBetItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListViewItem item)
        {
            return;
        }

        if (DataContext is MainViewModel { Configuration.EnableTicketAnimations: false })
        {
            item.Opacity = 1;
            item.RenderTransform = Transform.Identity;
            return;
        }

        var translateTransform = item.RenderTransform as TranslateTransform;
        if (translateTransform is null)
        {
            translateTransform = new TranslateTransform();
            item.RenderTransform = translateTransform;
        }

        item.RenderTransformOrigin = new Point(0.5, 0.5);
        item.Opacity = 0;
        translateTransform.Y = 18;

        var alternationIndex = ItemsControl.GetAlternationIndex(item);
        var beginTime = TimeSpan.FromMilliseconds(Math.Min(alternationIndex * 70, 420));

        var storyboard = new Storyboard
        {
            BeginTime = beginTime
        };

        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var slideAnimation = new DoubleAnimation
        {
            From = 18,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(opacityAnimation, item);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(UIElement.OpacityProperty));

        Storyboard.SetTarget(slideAnimation, item);
        Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

        storyboard.Children.Add(opacityAnimation);
        storyboard.Children.Add(slideAnimation);
        storyboard.Begin();
    }

    private void RecentBetItemMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not ListViewItem { DataContext: BetItemViewModel bet })
        {
            return;
        }

        if (DataContext is MainViewModel viewModel && viewModel.StartEditCommand.CanExecute(bet))
        {
            viewModel.StartEditCommand.Execute(bet);
            e.Handled = true;
        }
    }

    private void ElevatedCardMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        border.Effect = new DropShadowEffect
        {
            BlurRadius = 26,
            Opacity = 0.22,
            ShadowDepth = 0,
            Color = Color.FromRgb(2, 6, 23)
        };

        AnimateBorderTranslate(border, -3, 180);
    }

    private void ElevatedCardMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        border.Effect = null;
        AnimateBorderTranslate(border, 0, 220);
    }

    private void TicketCardMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        border.Effect = new DropShadowEffect
        {
            BlurRadius = 24,
            Opacity = 0.2,
            ShadowDepth = 0,
            Color = Color.FromRgb(2, 6, 23)
        };

        AnimateBorderTranslate(border, -4, 160);
    }

    private void TicketCardMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        border.Effect = null;
        AnimateBorderTranslate(border, 0, 200);
    }

    private static void AnimateBorderTranslate(Border border, double to, int durationMs)
    {
        var translateTransform = border.RenderTransform as TranslateTransform;
        if (translateTransform is null)
        {
            translateTransform = new TranslateTransform();
            border.RenderTransform = translateTransform;
        }

        var animation = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        translateTransform.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private void ActionButtonMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.Effect = new DropShadowEffect
        {
            BlurRadius = 18,
            Opacity = 0.2,
            ShadowDepth = 0,
            Color = Color.FromRgb(2, 6, 23)
        };

        AnimateControlScale(button, 1.02, 140);
    }

    private void ActionButtonMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.Effect = null;
        AnimateControlScale(button, 1, 180);
    }

    private void ActionButtonPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        AnimateControlScale(button, 0.97, 80);
    }

    private void ActionButtonPreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        AnimateControlScale(button, 1.02, 120);
    }

    private void InputGotFocus(object sender, RoutedEventArgs e)
    {
        switch (sender)
        {
            case Control control:
                control.BorderBrush = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                control.Effect = new DropShadowEffect
                {
                    BlurRadius = 16,
                    Opacity = 0.18,
                    ShadowDepth = 0,
                    Color = Color.FromRgb(249, 115, 22)
                };
                break;
        }
    }

    private void InputLostFocus(object sender, RoutedEventArgs e)
    {
        switch (sender)
        {
            case Control control:
                control.BorderBrush = new SolidColorBrush(Color.FromArgb(0x3A, 0xF8, 0xFA, 0xFC));
                control.Effect = null;
                break;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is MainViewModel newViewModel)
        {
            SubscribeToViewModel(newViewModel);
        }
    }

    private void SubscribeToViewModel(MainViewModel viewModel)
    {
        if (ReferenceEquals(subscribedViewModel, viewModel))
        {
            return;
        }

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        subscribedViewModel = viewModel;
        subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.StatusMessage))
        {
            Dispatcher.Invoke(AnimateStatusPanel);
        }
    }

    private void AnimateStatusPanel()
    {
        StatusPanel.RenderTransformOrigin = new Point(0.5, 0.5);

        if (StatusPanel.RenderTransform is not ScaleTransform scaleTransform)
        {
            scaleTransform = new ScaleTransform(1, 1);
            StatusPanel.RenderTransform = scaleTransform;
        }

        var storyboard = new Storyboard();

        var opacityAnimation = new DoubleAnimation
        {
            From = 0.72,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var scaleXAnimation = new DoubleAnimation
        {
            From = 0.985,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };

        var scaleYAnimation = new DoubleAnimation
        {
            From = 0.985,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };

        Storyboard.SetTarget(opacityAnimation, StatusPanel);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(UIElement.OpacityProperty));

        Storyboard.SetTarget(scaleXAnimation, StatusPanel);
        Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

        Storyboard.SetTarget(scaleYAnimation, StatusPanel);
        Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

        storyboard.Children.Add(opacityAnimation);
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);

        StatusPanel.Effect = new DropShadowEffect
        {
            BlurRadius = 18,
            Opacity = 0.18,
            ShadowDepth = 0,
            Color = Color.FromRgb(251, 191, 36)
        };

        storyboard.Completed += (_, _) => StatusPanel.Effect = null;
        storyboard.Begin();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            subscribedViewModel = null;
        }

        base.OnClosed(e);
    }

    private static void AnimateControlScale(Control control, double to, int durationMs)
    {
        var scaleTransform = control.RenderTransform as ScaleTransform;
        if (scaleTransform is null)
        {
            scaleTransform = new ScaleTransform(1, 1);
            control.RenderTransform = scaleTransform;
            control.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var animation = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
