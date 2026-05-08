using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MemBooster;

public sealed class TimedWarningDialog : Window
{
    private readonly Button _confirmButton;
    private readonly TextBlock _countdownText;
    private readonly DispatcherTimer _timer;
    private int _secondsRemaining;

    public TimedWarningDialog(string title, string message, string confirmButtonText, int delaySeconds)
    {
        _secondsRemaining = Math.Max(1, delaySeconds);

        Title = title;
        Width = 560;
        MinWidth = 500;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(11, 15, 23));
        Foreground = new SolidColorBrush(Color.FromRgb(238, 245, 255));
        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI");

        var root = new Border
        {
            Padding = new Thickness(22),
            Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(37, 50, 71)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12)
        };

        var stack = new StackPanel();
        root.Child = stack;

        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 12)
        });

        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            Margin = new Thickness(0, 0, 0, 16)
        });

        _countdownText = new TextBlock
        {
            Text = $"Please review. Continue available in {_secondsRemaining}s.",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
            Margin = new Thickness(0, 0, 0, 18)
        };
        stack.Children.Add(_countdownText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = CreateButton("Cancel", false);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        _confirmButton = CreateButton(confirmButtonText, true);
        _confirmButton.IsEnabled = false;
        _confirmButton.Content = $"{confirmButtonText} ({_secondsRemaining})";
        _confirmButton.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(_confirmButton);
        stack.Children.Add(buttons);

        Content = root;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick(confirmButtonText);

        Loaded += (_, _) => _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = primary ? 130 : 90,
            Height = 38,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(14, 7, 14, 7),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(primary ? Color.FromRgb(15, 81, 50) : Color.FromRgb(29, 39, 56)),
            Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            BorderBrush = new SolidColorBrush(primary ? Color.FromRgb(34, 197, 94) : Color.FromRgb(37, 50, 71)),
            BorderThickness = new Thickness(1)
        };
        return button;
    }

    private void Tick(string confirmButtonText)
    {
        _secondsRemaining--;
        if (_secondsRemaining > 0)
        {
            _countdownText.Text = $"Please review. Continue available in {_secondsRemaining}s.";
            _confirmButton.Content = $"{confirmButtonText} ({_secondsRemaining})";
            return;
        }

        _timer.Stop();
        _countdownText.Text = "Ready. You can continue or cancel.";
        _confirmButton.Content = confirmButtonText;
        _confirmButton.IsEnabled = true;
        _confirmButton.Focus();
    }
}
