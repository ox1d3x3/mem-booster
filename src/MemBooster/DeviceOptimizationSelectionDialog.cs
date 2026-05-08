using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MemBooster.Services;

namespace MemBooster;

public sealed class DeviceOptimizationSelectionDialog : Window
{
    private readonly List<OptionRow> _rows = new();
    private readonly Button _confirmButton;
    private readonly TextBlock _countdownText;
    private readonly TextBlock _selectedText;
    private readonly DispatcherTimer _timer;
    private readonly string _confirmButtonText;
    private int _secondsRemaining;
    private bool _countdownComplete;

    public DeviceOptimizationSelectionDialog(
        string title,
        string introduction,
        IReadOnlyList<DeviceOptimizationOption> options,
        string confirmButtonText,
        int delaySeconds)
    {
        _confirmButtonText = confirmButtonText;
        _secondsRemaining = Math.Max(1, delaySeconds);

        Title = title;
        Width = 760;
        Height = 680;
        MinWidth = 680;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = Brush(11, 15, 23);
        Foreground = Brush(238, 245, 255);
        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI");

        var root = new Border
        {
            Padding = new Thickness(22),
            Background = Brush(17, 24, 39),
            BorderBrush = Brush(37, 50, 71),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Child = grid;

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(248, 250, 252)
        });
        header.Children.Add(new TextBlock
        {
            Text = introduction,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
            Foreground = Brush(203, 213, 225),
            Margin = new Thickness(0, 8, 0, 0)
        });
        grid.Children.Add(header);

        var actionBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetRow(actionBar, 1);
        var quickButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var recommendedButton = CreateButton("Recommended", false, 110);
        recommendedButton.Click += (_, _) => SetChecked(options.Where(option => option.DefaultSelected).Select(option => option.Id));
        var allButton = CreateButton("Select all", false, 90);
        allButton.Click += (_, _) => SetChecked(options.Select(option => option.Id));
        var clearButton = CreateButton("Clear", false, 75);
        clearButton.Click += (_, _) => SetChecked(Array.Empty<string>());
        quickButtons.Children.Add(recommendedButton);
        quickButtons.Children.Add(allButton);
        quickButtons.Children.Add(clearButton);
        actionBar.Children.Add(quickButtons);

        _selectedText = new TextBlock
        {
            Text = "0 selected",
            Foreground = Brush(96, 165, 250),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(_selectedText, Dock.Right);
        actionBar.Children.Add(_selectedText);
        grid.Children.Add(actionBar);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brush(14, 21, 33)
        };
        Grid.SetRow(scroll, 2);
        var stack = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        scroll.Content = stack;

        foreach (var option in options)
        {
            var row = AddOptionRow(stack, option);
            _rows.Add(row);
        }
        grid.Children.Add(scroll);

        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 3);

        _countdownText = new TextBlock
        {
            Text = $"Please review. Continue available in {_secondsRemaining}s.",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(96, 165, 250),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };
        footer.Children.Add(_countdownText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(buttons, 1);

        var cancelButton = CreateButton("Cancel", false, 90);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        _confirmButton = CreateButton(confirmButtonText, true, 190);
        _confirmButton.IsEnabled = false;
        _confirmButton.Content = $"{confirmButtonText} ({_secondsRemaining})";
        _confirmButton.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(_confirmButton);
        footer.Children.Add(buttons);
        grid.Children.Add(footer);

        Content = root;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) =>
        {
            UpdateSelectionText();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    public IReadOnlyList<string> SelectedOptionIds => _rows
        .Where(row => row.CheckBox.IsChecked == true)
        .Select(row => row.Option.Id)
        .ToArray();

    private OptionRow AddOptionRow(Panel stack, DeviceOptimizationOption option)
    {
        var border = new Border
        {
            Background = Brush(15, 23, 36),
            BorderBrush = Brush(37, 50, 71),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        border.Child = grid;

        var checkBox = new CheckBox
        {
            IsChecked = option.DefaultSelected,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 12, 0),
            Width = 28,
            Height = 28,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        checkBox.Click += (_, eventArgs) => eventArgs.Handled = true;
        checkBox.Checked += (_, _) => UpdateSelectionText();
        checkBox.Unchecked += (_, _) => UpdateSelectionText();
        grid.Children.Add(checkBox);

        var textStack = new StackPanel();
        Grid.SetColumn(textStack, 1);

        var titleLine = new StackPanel { Orientation = Orientation.Horizontal };
        titleLine.Children.Add(new TextBlock
        {
            Text = option.Name,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(248, 250, 252)
        });
        titleLine.Children.Add(new TextBlock
        {
            Text = $"  {option.Category}",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(34, 197, 94),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(4, 0, 0, 1)
        });
        if (option.Advanced || option.RequiresRestart || !string.IsNullOrWhiteSpace(option.WarningText))
        {
            titleLine.Children.Add(new TextBlock
            {
                Text = option.RequiresRestart ? "  ⚠ restart" : "  ⚠ review",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(251, 191, 36),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4, 0, 0, 1)
            });
        }
        textStack.Children.Add(titleLine);

        textStack.Children.Add(new TextBlock
        {
            Text = option.Description,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Brush(203, 213, 225),
            Margin = new Thickness(0, 5, 0, 0)
        });

        if (!string.IsNullOrWhiteSpace(option.WarningText))
        {
            textStack.Children.Add(new TextBlock
            {
                Text = "⚠ " + option.WarningText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = Brush(251, 191, 36),
                Margin = new Thickness(0, 5, 0, 0)
            });
        }

        grid.Children.Add(textStack);
        border.MouseLeftButtonUp += (_, eventArgs) =>
        {
            if (IsInsideCheckBox(eventArgs.OriginalSource as DependencyObject))
            {
                return;
            }

            checkBox.IsChecked = checkBox.IsChecked != true;
        };
        stack.Children.Add(border);
        return new OptionRow(option, checkBox);
    }


    private static bool IsInsideCheckBox(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is CheckBox)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void SetChecked(IEnumerable<string> selectedIds)
    {
        var set = selectedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows)
        {
            row.CheckBox.IsChecked = set.Contains(row.Option.Id);
        }

        UpdateSelectionText();
    }

    private void UpdateSelectionText()
    {
        var count = _rows.Count(row => row.CheckBox.IsChecked == true);
        _selectedText.Text = count == 1 ? "1 selected" : $"{count} selected";
        _confirmButton.IsEnabled = _countdownComplete && count > 0;
    }

    private static Button CreateButton(string text, bool primary, double minWidth)
    {
        return new Button
        {
            Content = text,
            MinWidth = minWidth,
            Height = 38,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(14, 7, 14, 7),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = Brush(primary ? 15 : 29, primary ? 81 : 39, primary ? 50 : 56),
            Foreground = Brush(255, 255, 255),
            BorderBrush = Brush(primary ? 34 : 37, primary ? 197 : 50, primary ? 94 : 71),
            BorderThickness = new Thickness(1)
        };
    }

    private void Tick()
    {
        _secondsRemaining--;
        if (_secondsRemaining > 0)
        {
            _countdownText.Text = $"Please review. Continue available in {_secondsRemaining}s.";
            _confirmButton.Content = $"{_confirmButtonText} ({_secondsRemaining})";
            return;
        }

        _timer.Stop();
        _countdownComplete = true;
        _countdownText.Text = "Ready. Select the optimisations you want, then continue or cancel.";
        _confirmButton.Content = _confirmButtonText;
        UpdateSelectionText();
        _confirmButton.Focus();
    }

    private static SolidColorBrush Brush(int r, int g, int b) => new(Color.FromRgb((byte)r, (byte)g, (byte)b));

    private sealed record OptionRow(DeviceOptimizationOption Option, CheckBox CheckBox);
}
