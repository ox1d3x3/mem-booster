using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MemBooster.Models;
using MemBooster.Services;
using Microsoft.Win32;

namespace MemBooster;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ProcessService _processService = new();
    private readonly MemoryService _memoryService = new();
    private readonly ProfileService _profileService = new();
    private readonly UpdateService _updateService = new();
    private readonly DiagnosticsService _diagnosticsService = new();
    private readonly DeviceOptimizationService _deviceOptimizationService;
    private readonly LoggerService _loggerService;
    private readonly DispatcherTimer _memoryTimer;
    private readonly DispatcherTimer _processTimer;
    private readonly HashSet<string> _selectedProcessNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ICollectionView _processView;

    private bool _refreshRunning;
    private bool _syncingSelection;
    private bool _busyOperation;
    private DateTime _suppressRowToggleUntilUtc = DateTime.MinValue;
    private string _memoryText = "Loading...";
    private double _memoryPercent;
    private string _runningText = "0";
    private string _selectedText = "0 active / 0 profile";
    private string _profileStatus = "No profile loaded";
    private string _statusText = "Ready";
    private string _searchText = string.Empty;
    private readonly bool _isAdministrator;
    private string _adminModeText = "Standard mode";
    private bool _isRunAsAdminButtonEnabled = true;
    private const string CurrentVersion = "0.5.22";
    private string _currentTheme = "Dark";
    private string _updateButtonText = "Updates";
    private bool _checkingForUpdates;
    private bool _isRevertDeviceOptimiseEnabled;
    private string _operationProgressText = "Ready";
    private string _operationElapsedText = string.Empty;
    private double _operationProgressValue;
    private Visibility _operationProgressVisibility = Visibility.Collapsed;
    private readonly DispatcherTimer _progressTimer;
    private Stopwatch? _operationStopwatch;
    private UpdateCheckResult? _lastUpdateCheck;

    public ObservableCollection<ProcessGroup> ProcessGroups { get; } = new();

    public ObservableCollection<SelectedAppItem> SelectedApps { get; } = new();

    public ICollectionView ProcessView => _processView;

    public string MemoryText
    {
        get => _memoryText;
        set => SetField(ref _memoryText, value);
    }

    public double MemoryPercent
    {
        get => _memoryPercent;
        set => SetField(ref _memoryPercent, value);
    }

    public string RunningText
    {
        get => _runningText;
        set => SetField(ref _runningText, value);
    }

    public string SelectedText
    {
        get => _selectedText;
        set => SetField(ref _selectedText, value);
    }

    public string ProfileStatus
    {
        get => _profileStatus;
        set => SetField(ref _profileStatus, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string AdminModeText
    {
        get => _adminModeText;
        set => SetField(ref _adminModeText, value);
    }

    public bool IsRunAsAdminButtonEnabled
    {
        get => _isRunAsAdminButtonEnabled;
        set => SetField(ref _isRunAsAdminButtonEnabled, value);
    }

    public string UpdateButtonText
    {
        get => _updateButtonText;
        set => SetField(ref _updateButtonText, value);
    }


    public bool IsRevertDeviceOptimiseEnabled
    {
        get => _isRevertDeviceOptimiseEnabled;
        set => SetField(ref _isRevertDeviceOptimiseEnabled, value);
    }

    public string OperationProgressText
    {
        get => _operationProgressText;
        set => SetField(ref _operationProgressText, value);
    }

    public string OperationElapsedText
    {
        get => _operationElapsedText;
        set => SetField(ref _operationElapsedText, value);
    }

    public double OperationProgressValue
    {
        get => _operationProgressValue;
        set => SetField(ref _operationProgressValue, value);
    }

    public Visibility OperationProgressVisibility
    {
        get => _operationProgressVisibility;
        set => SetField(ref _operationProgressVisibility, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        SetWindowIconSafely();
        _deviceOptimizationService = new DeviceOptimizationService(_profileService.AppDataDirectory);
        _loggerService = new LoggerService(_profileService.AppDataDirectory);
        _isAdministrator = AdminService.IsCurrentProcessElevated();
        _loggerService.Write($"Mem-Booster v{CurrentVersion} startup; elevated={_isAdministrator}; baseDirectory={AppContext.BaseDirectory}; appData={_profileService.AppDataDirectory}");
        try
        {
            _profileService.ClearRestoreSession();
            _loggerService.Write("Legacy restore session cleared. Mem-Booster now recommends restarting Windows after boosting instead of reopening apps automatically.");
        }
        catch (Exception ex)
        {
            _loggerService.Write($"Could not clear legacy restore session: {ex.Message}");
        }
        AdminModeText = _isAdministrator ? "Administrator mode" : "Standard mode";
        IsRunAsAdminButtonEnabled = !_isAdministrator;
        ApplySavedTheme();

        _processView = CollectionViewSource.GetDefaultView(ProcessGroups);
        _processView.Filter = FilterProcess;
        _processView.SortDescriptions.Add(new SortDescription(nameof(ProcessGroup.WorkingSetBytes), ListSortDirection.Descending));
        _processView.SortDescriptions.Add(new SortDescription(nameof(ProcessGroup.DisplayName), ListSortDirection.Ascending));

        DataContext = this;

        LoadLocalProfileQuietly();
        UpdateDeviceOptimiseButtonState();
        UpdateMemoryInfo();

        _memoryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _memoryTimer.Tick += (_, _) => UpdateMemoryInfo();

        _processTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _processTimer.Tick += async (_, _) =>
        {
            if (!_busyOperation && AutoRefreshCheckBox.IsChecked == true && !ProcessesGrid.IsKeyboardFocusWithin)
            {
                await RefreshProcessesAsync();
            }
        };

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _progressTimer.Tick += (_, _) => UpdateOperationElapsedText();

        Loaded += async (_, _) =>
        {
            await RunInitialLoadAsync();
        };
    }


    private void SetWindowIconSafely()
    {
        try
        {
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/mem-booster.ico", UriKind.Absolute));
        }
        catch (Exception ex)
        {
            App.WriteStartupLog("Window icon could not be loaded. Continuing without custom window icon.", ex);
        }
    }

    private void ApplySavedTheme()
    {
        var savedTheme = _profileService.LoadThemePreference();
        ApplyTheme(savedTheme, animateToggle: false);
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        using var operation = _loggerService.BeginOperation("ThemeToggle", $"currentTheme={_currentTheme}");
        e.Handled = true;
        SuppressRowToggle(650);

        var processTimer = _processTimer;
        var wasProcessTimerRunning = processTimer.IsEnabled;
        if (wasProcessTimerRunning)
        {
            processTimer.Stop();
        }

        try
        {
            var nextTheme = string.Equals(_currentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                ? "Dark"
                : "Light";

            ApplyTheme(nextTheme, animateToggle: true);
            operation.Checkpoint($"Theme applied: {nextTheme}");

            try
            {
                _profileService.SaveThemePreference(_currentTheme);
            }
            catch
            {
                // Theme preference is convenience-only. Do not interrupt the user if saving fails.
            }
        }
        finally
        {
            if (wasProcessTimerRunning && AutoRefreshCheckBox.IsChecked == true && !_busyOperation)
            {
                processTimer.Start();
            }
        }
    }

    private void ApplyTheme(string theme, bool animateToggle = false)
    {
        var light = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        _currentTheme = light ? "Light" : "Dark";

        if (light)
        {
            SetBrush("BackgroundBrush", "#F4F7FB");
            SetBrush("HeaderBrush", "#FFFFFF");
            SetBrush("PanelBrush", "#FFFFFF");
            SetBrush("CardBrush", "#FFFFFF");
            SetBrush("CardAltBrush", "#F7FAFD");
            SetBrush("BorderBrushSoft", "#D8E0EC");
            SetBrush("TextBrush", "#101827");
            SetBrush("TextMutedBrush", "#526071");
            SetBrush("AccentBrush", "#0E9F6E");
            SetBrush("AccentBlueBrush", "#2563EB");
            SetBrush("DangerBrush", "#DC2626");
            SetBrush("ButtonBrush", "#EEF3FA");
            SetBrush("ButtonHoverBrush", "#E1EAF6");
            SetBrush("InputBrush", "#F8FAFC");
            SetBrush("DataGridHeaderBrush", "#EEF3FA");
            SetBrush("DataGridRowBrush", "#FFFFFF");
            SetBrush("DataGridAltRowBrush", "#F8FAFC");
            SetBrush("DataGridGridBrush", "#E2E8F0");
            SetBrush("BoostButtonBrush", "#0E9F6E");
            SetBrush("BoostButtonBorderBrush", "#0E9F6E");
            SetBrush("LogoPanelBrush", "#F8FAFC");
        }
        else
        {
            SetBrush("BackgroundBrush", "#090D14");
            SetBrush("HeaderBrush", "#0D1320");
            SetBrush("PanelBrush", "#101722");
            SetBrush("CardBrush", "#151D2A");
            SetBrush("CardAltBrush", "#182231");
            SetBrush("BorderBrushSoft", "#253247");
            SetBrush("TextBrush", "#EEF5FF");
            SetBrush("TextMutedBrush", "#9DAABE");
            SetBrush("AccentBrush", "#28D17C");
            SetBrush("AccentBlueBrush", "#48A6FF");
            SetBrush("DangerBrush", "#FF5C7A");
            SetBrush("ButtonBrush", "#202B3D");
            SetBrush("ButtonHoverBrush", "#2A3850");
            SetBrush("InputBrush", "#0D1420");
            SetBrush("DataGridHeaderBrush", "#172033");
            SetBrush("DataGridRowBrush", "#111925");
            SetBrush("DataGridAltRowBrush", "#0F1722");
            SetBrush("DataGridGridBrush", "#1D293B");
            SetBrush("BoostButtonBrush", "#123D2B");
            SetBrush("BoostButtonBorderBrush", "#28D17C");
            SetBrush("LogoPanelBrush", "#0B111D");
        }

        Background = (Brush)Resources["BackgroundBrush"];
        Foreground = (Brush)Resources["TextBrush"];
        UpdateThemeToggle(light, animateToggle);
    }

    private void UpdateThemeToggle(bool light, bool animate)
    {
        if (ThemeIconPath is null || ThemeToggleButton is null)
        {
            return;
        }

        var sunGeometry = "M10,3 A7,7 0 1 1 10,17 A7,7 0 1 1 10,3 M10,0.5 L10,2.3 M10,17.7 L10,19.5 M0.5,10 L2.3,10 M17.7,10 L19.5,10 M3.1,3.1 L4.4,4.4 M15.6,15.6 L16.9,16.9 M16.9,3.1 L15.6,4.4 M4.4,15.6 L3.1,16.9";
        var moonGeometry = "M18,13.5 C16.4,15.4 14,16.6 11.3,16.6 C6.4,16.6 2.4,12.6 2.4,7.7 C2.4,5.1 3.5,2.7 5.3,1 C5.1,1.8 5,2.6 5,3.4 C5,8.4 9,12.4 14,12.4 C15.4,12.4 16.8,12.1 18,11.4 C18.2,12.1 18.2,12.8 18,13.5 Z";

        ThemeIconPath.Data = Geometry.Parse(light ? sunGeometry : moonGeometry);
        ThemeIconPath.Fill = light ? Brushes.Transparent : (Brush)Resources["AccentBrush"];
        ThemeIconPath.Stroke = (Brush)Resources["AccentBrush"];
        ThemeIconPath.StrokeThickness = light ? 1.8 : 1.6;
        ThemeToggleButton.ToolTip = light ? "Switch to dark theme" : "Switch to light theme";

        if (!animate)
        {
            if (ThemeIconScale is not null)
            {
                ThemeIconScale.ScaleX = 1;
                ThemeIconScale.ScaleY = 1;
            }
            if (ThemeIconRotate is not null)
            {
                ThemeIconRotate.Angle = light ? 0 : -18;
            }
            return;
        }

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(180);

        if (ThemeIconRotate is not null)
        {
            ThemeIconRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(light ? 0 : -18, duration)
            {
                EasingFunction = easing
            });
        }

        if (ThemeIconScale is not null)
        {
            ThemeIconScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.85, 1, duration)
            {
                EasingFunction = easing
            });
            ThemeIconScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.85, 1, duration)
            {
                EasingFunction = easing
            });
        }
    }

    private void SetBrush(string resourceKey, string hex)
    {
        var colour = (Color)ColorConverter.ConvertFromString(hex);

        // Some WPF brushes can be frozen/read-only once loaded from XAML or referenced by styles.
        // Replacing the resource keeps DynamicResource bindings working and avoids startup crashes.
        Resources[resourceKey] = new SolidColorBrush(colour);
    }

    private async Task RunInitialLoadAsync()
    {
        using var operation = _loggerService.BeginOperation("InitialLoad", $"version={CurrentVersion}; selected={_selectedProcessNames.Count}");
        SetBusyState(true, "Loading Mem-Booster...");
        ShowOperationProgress("Starting Mem-Booster...", 5);

        try
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            UpdateOperationProgress("Scanning running apps...", 25);
            await RefreshProcessesAsync(force: true, showProgress: true, progressContext: "Initial load");
            UpdateOperationProgress($"Loaded {ProcessGroups.Count} running app groups.", 90);
            operation.Checkpoint($"Initial refresh complete; groups={ProcessGroups.Count}; selected={_selectedProcessNames.Count}");

            _memoryTimer.Start();
            _processTimer.Start();
            UpdateOperationProgress("Ready", 100);
            StatusText = $"Ready. Loaded {ProcessGroups.Count} running app groups.";

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(8));
                    await Dispatcher.InvokeAsync(() => { _ = CheckForUpdatesAsync(silent: true); });
                }
                catch
                {
                    // Update checks must never affect startup smoothness.
                }
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Initial load failed: {ex.Message}";
            _loggerService.Write($"Initial load failed: {ex}");
        }
        finally
        {
            await HideOperationProgressAfterDelayAsync(450);
            SetBusyState(false);
        }
    }

    private void ShowOperationProgress(string text, double value = 0)
    {
        _operationStopwatch = Stopwatch.StartNew();
        OperationProgressVisibility = Visibility.Visible;
        OperationProgressText = text;
        OperationProgressValue = Math.Clamp(value, 0, 100);
        OperationElapsedText = "0.0s";
        if (!_progressTimer.IsEnabled)
        {
            _progressTimer.Start();
        }
        _loggerService.Write($"Progress shown: {text}; value={OperationProgressValue:0}");
    }

    private void UpdateOperationProgress(string text, double value)
    {
        OperationProgressText = text;
        OperationProgressValue = Math.Clamp(value, 0, 100);
        StatusText = text;
        UpdateOperationElapsedText();
        _loggerService.Write($"Progress update: {text}; value={OperationProgressValue:0}; elapsed={OperationElapsedText}");
    }

    private void UpdateOperationElapsedText()
    {
        if (_operationStopwatch is null)
        {
            OperationElapsedText = string.Empty;
            return;
        }

        OperationElapsedText = $"{_operationStopwatch.Elapsed.TotalSeconds:0.0}s";
    }

    private async Task HideOperationProgressAfterDelayAsync(int delayMs = 300)
    {
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }

        _progressTimer.Stop();
        _operationStopwatch?.Stop();
        _operationStopwatch = null;
        OperationProgressVisibility = Visibility.Collapsed;
        OperationProgressText = "Ready";
        OperationProgressValue = 0;
        OperationElapsedText = string.Empty;
    }

    private bool ShowTimedWarning(string title, string message, string confirmButtonText, int delaySeconds = 5)
    {
        _loggerService.Write($"Timed warning shown: title={title}; delaySeconds={delaySeconds}");
        var dialog = new TimedWarningDialog(title, message, confirmButtonText, delaySeconds)
        {
            Owner = this
        };

        var accepted = dialog.ShowDialog() == true;
        _loggerService.Write($"Timed warning result: title={title}; accepted={accepted}");
        return accepted;
    }

    private void LoadLocalProfileQuietly()
    {
        try
        {
            var profile = _profileService.LoadLocalProfile();
            _selectedProcessNames.Clear();
            var dropped = 0;
            foreach (var name in profile.ExecutableNames.Select(SafetyRules.NormaliseProcessName))
            {
                if (SafetyRules.IsAutoLoadProfileAllowed(name))
                {
                    _selectedProcessNames.Add(name);
                }
                else
                {
                    dropped++;
                    _loggerService.Write($"Local profile sanitised legacy/unsafe entry: {name}");
                }
            }

            ProfileStatus = _selectedProcessNames.Count > 0
                ? dropped > 0
                    ? $"Loaded local profile: {_selectedProcessNames.Count} entries, {dropped} legacy/unsafe removed"
                    : $"Loaded local profile: {_selectedProcessNames.Count} entries"
                : "No local profile yet. Select apps and save.";
        }
        catch (Exception ex)
        {
            ProfileStatus = $"Local profile could not be loaded: {ex.Message}";
        }
    }


    private void UpdateDeviceOptimiseButtonState()
    {
        try
        {
            IsRevertDeviceOptimiseEnabled = _deviceOptimizationService.HasActiveState();
        }
        catch
        {
            IsRevertDeviceOptimiseEnabled = false;
        }
    }

    private HashSet<string> GetSaveableProfileNames()
    {
        return _selectedProcessNames
            .Select(SafetyRules.NormaliseProcessName)
            .Where(SafetyRules.IsAutoLoadProfileAllowed)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task RefreshProcessesAsync(bool force = false, bool showProgress = false, string? progressContext = null)
    {
        if (_refreshRunning && !force)
        {
            return;
        }

        _refreshRunning = true;
        var refreshWatch = Stopwatch.StartNew();
        using var operation = _loggerService.BeginOperation("RefreshProcesses", $"force={force}; selected={_selectedProcessNames.Count}; progress={showProgress}; context={progressContext ?? "auto"}");
        StatusText = "Refreshing running apps...";
        if (showProgress)
        {
            if (OperationProgressVisibility != Visibility.Visible)
            {
                ShowOperationProgress(progressContext ?? "Refreshing running apps...", 10);
            }
            else
            {
                UpdateOperationProgress(progressContext ?? "Refreshing running apps...", Math.Max(OperationProgressValue, 10));
            }
        }

        try
        {
            if (showProgress)
            {
                UpdateOperationProgress("Reading process snapshot...", 25);
            }

            var snapshots = await Task.Run(() => _processService.GetProcessGroups(message => operation.Checkpoint(message)));

            if (showProgress)
            {
                UpdateOperationProgress($"Updating UI with {snapshots.Count} app group(s)...", 65);
                await Dispatcher.Yield(DispatcherPriority.Background);
            }
            var incomingNames = snapshots.Select(s => s.ExeName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            _syncingSelection = true;
            try
            {
                for (var i = ProcessGroups.Count - 1; i >= 0; i--)
                {
                    if (!incomingNames.Contains(ProcessGroups[i].ExeName))
                    {
                        ProcessGroups[i].PropertyChanged -= ProcessGroup_PropertyChanged;
                        ProcessGroups.RemoveAt(i);
                    }
                }

                var existing = ProcessGroups.ToDictionary(p => p.ExeName, StringComparer.OrdinalIgnoreCase);

                foreach (var snapshot in snapshots)
                {
                    if (!existing.TryGetValue(snapshot.ExeName, out var group))
                    {
                        group = new ProcessGroup(snapshot.ExeName);
                        group.UpdateFrom(snapshot, _selectedProcessNames.Contains(snapshot.ExeName));
                        group.PropertyChanged += ProcessGroup_PropertyChanged;
                        ProcessGroups.Add(group);
                    }
                    else
                    {
                        group.UpdateFrom(snapshot, _selectedProcessNames.Contains(snapshot.ExeName));
                    }
                }
            }
            finally
            {
                _syncingSelection = false;
            }

            RunningText = ProcessGroups.Count.ToString();
            UpdateSelectionSummary();
            _processView.Refresh();
            refreshWatch.Stop();
            StatusText = $"Updated {DateTime.Now:T} in {refreshWatch.ElapsedMilliseconds} ms";
            if (showProgress)
            {
                UpdateOperationProgress($"Loaded {ProcessGroups.Count} app groups in {refreshWatch.ElapsedMilliseconds} ms", 100);
            }
            operation.Checkpoint($"Refresh UI updated: visibleGroups={ProcessGroups.Count}; selected={_selectedProcessNames.Count}; elapsedMs={refreshWatch.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            StatusText = $"Refresh failed: {ex.Message}";
            _loggerService.Write($"Refresh failed: {ex}");
        }
        finally
        {
            _refreshRunning = false;
            if (showProgress && !string.Equals(progressContext, "Initial load", StringComparison.OrdinalIgnoreCase) && !_busyOperation)
            {
                await HideOperationProgressAfterDelayAsync(250);
            }
        }
    }

    private bool FilterProcess(object item)
    {
        if (item is not ProcessGroup process)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return true;
        }

        return process.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || process.ExeName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || process.RiskLabel.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void ProcessGroup_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncingSelection || sender is not ProcessGroup process || e.PropertyName != nameof(ProcessGroup.IsSelected))
        {
            return;
        }

        if (process.IsSelected)
        {
            _selectedProcessNames.Add(process.ExeName);
            _loggerService.Write($"Selection changed: {process.DisplayName} | {process.ExeName} | selected=True");
            StatusText = $"Selected {process.DisplayName}.";
        }
        else
        {
            _selectedProcessNames.Remove(process.ExeName);
            _loggerService.Write($"Selection changed: {process.DisplayName} | {process.ExeName} | selected=False");
            StatusText = $"Removed {process.DisplayName}.";
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var activeMatches = ProcessGroups.Count(p => p.IsSelected);
        SelectedText = $"{activeMatches} active / {_selectedProcessNames.Count} profile";

        SelectedApps.Clear();
        foreach (var exeName in _selectedProcessNames.OrderBy(GetSelectedDisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var running = ProcessGroups.FirstOrDefault(p => p.ExeName.Equals(exeName, StringComparison.OrdinalIgnoreCase));
            var displayName = running?.DisplayName ?? ToFriendlyFallbackName(exeName);
            var state = running is null
                ? "Not running"
                : $"{running.ExeName} • {running.MemoryText}";

            SelectedApps.Add(new SelectedAppItem(displayName, exeName, state));
        }
    }

    private string GetSelectedDisplayName(string exeName)
    {
        return ProcessGroups.FirstOrDefault(p => p.ExeName.Equals(exeName, StringComparison.OrdinalIgnoreCase))?.DisplayName
            ?? ToFriendlyFallbackName(exeName);
    }

    private static string ToFriendlyFallbackName(string exeName)
    {
        var name = exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? exeName[..^4]
            : exeName;

        return string.Join(" ", name.Replace('_', ' ').Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length <= 1 ? part.ToUpperInvariant() : char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private void UpdateMemoryInfo()
    {
        try
        {
            var memory = _memoryService.GetMemoryInfo();
            MemoryText = memory.Summary;
            MemoryPercent = memory.UsedPercent;
        }
        catch
        {
            MemoryText = "Memory unavailable";
            MemoryPercent = 0;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        UpdateMemoryInfo();
        await RefreshProcessesAsync(force: true, showProgress: true, progressContext: "Manual refresh");
    }

    private void SaveLocalButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        try
        {
            var saveableNames = GetSaveableProfileNames();
            _profileService.SaveLocalProfile(saveableNames);
            var dropped = _selectedProcessNames.Count - saveableNames.Count;
            _loggerService.Write($"Local profile saved; entries={saveableNames.Count}; skippedUnsafeOrTemporary={dropped}");
            ProfileStatus = dropped > 0
                ? $"Saved local profile: {saveableNames.Count} entries, {dropped} temporary/unsafe skipped"
                : $"Saved local profile: {saveableNames.Count} entries";
            StatusText = "Local profile saved.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save Local Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportXmlButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        if (_selectedProcessNames.Count == 0)
        {
            MessageBox.Show(this, "Select at least one app before exporting a profile.", "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Mem-Booster XML Profile",
            Filter = "Mem-Booster XML profile (*.xml)|*.xml|All files (*.*)|*.*",
            FileName = "gaming-boost-profile.xml",
            AddExtension = true,
            DefaultExt = ".xml"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var saveableNames = GetSaveableProfileNames();
            var dropped = _selectedProcessNames.Count - saveableNames.Count;
            _profileService.SaveProfile(dialog.FileName, new ProfileData("Gaming Boost", saveableNames));
            _loggerService.Write($"XML profile exported; entries={saveableNames.Count}; skippedUnsafeOrTemporary={dropped}; path={dialog.FileName}");
            ProfileStatus = dropped > 0
                ? $"Exported XML profile: {saveableNames.Count} entries, {dropped} temporary/unsafe skipped"
                : $"Exported XML profile: {saveableNames.Count} entries";
            StatusText = $"Exported: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void LoadXmlButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        var dialog = new OpenFileDialog
        {
            Title = "Load Mem-Booster XML Profile",
            Filter = "Mem-Booster XML profile (*.xml)|*.xml|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var profile = _profileService.LoadProfile(dialog.FileName);
            _selectedProcessNames.Clear();
            var dropped = 0;
            foreach (var name in profile.ExecutableNames.Select(SafetyRules.NormaliseProcessName))
            {
                if (SafetyRules.IsAutoLoadProfileAllowed(name))
                {
                    _selectedProcessNames.Add(name);
                }
                else
                {
                    dropped++;
                    _loggerService.Write($"XML profile sanitised legacy/unsafe entry: {name}");
                }
            }

            ApplySelectionToRunningRows();
            _loggerService.WriteSelection($"LoadXml:{profile.Name}", _selectedProcessNames, ProcessGroups);
            await RefreshProcessesAsync(force: true);
            var activeMatches = ProcessGroups.Count(p => p.IsSelected);
            ProfileStatus = dropped > 0
                ? $"Loaded XML: {profile.Name}. {activeMatches} active matches, {_selectedProcessNames.Count - activeMatches} skipped/not running, {dropped} legacy/unsafe removed."
                : $"Loaded XML: {profile.Name}. {activeMatches} active matches, {_selectedProcessNames.Count - activeMatches} skipped/not running.";
            StatusText = "XML profile loaded.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Load XML Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectRecommendedButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        var matches = ProcessGroups.Where(p => p.CanSelect && p.IsRecommended).ToList();
        _selectedProcessNames.Clear();
        foreach (var process in matches)
        {
            _selectedProcessNames.Add(process.ExeName);
        }

        ApplySelectionToRunningRows();
        _loggerService.WriteSelection("SafeSelect", _selectedProcessNames, ProcessGroups);
        ProfileStatus = $"Safe list selected {matches.Count} running app(s). Gaming launchers, drivers, overlays, Discord and tuning tools are excluded.";
        StatusText = "Safe list applied.";
    }

    private void ExtremeSelectButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        var matches = ProcessGroups
            .Where(p => p.CanSelect && SafetyRules.IsExtremeRecommendedForGamingBoost(p.ExeName))
            .ToList();

        _selectedProcessNames.Clear();
        foreach (var process in matches)
        {
            _selectedProcessNames.Add(process.ExeName);
        }

        ApplySelectionToRunningRows();
        _loggerService.WriteSelection("ExtremeSelect", _selectedProcessNames, ProcessGroups);
        ProfileStatus = $"Extreme selected {matches.Count} running app(s). Non-running or missing apps are skipped automatically. Gaming launchers, drivers, overlays, Discord and tuning tools stay excluded.";
        StatusText = "Extreme list applied.";
    }

    private void AggressiveSelectButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        var matches = ProcessGroups
            .Where(p => p.CanSelect && SafetyRules.IsAggressiveRecommendedForGamingBoost(p.ExeName))
            .ToList();

        _selectedProcessNames.Clear();
        foreach (var process in matches)
        {
            _selectedProcessNames.Add(process.ExeName);
        }

        ApplySelectionToRunningRows();
        _loggerService.WriteSelection("AggressiveSelect", _selectedProcessNames, ProcessGroups);
        ProfileStatus = $"Aggressive selected {matches.Count} running app(s). Missing apps are skipped. Gaming, driver, overlay and core Windows processes stay excluded.";
        StatusText = "Aggressive list applied. Review selected apps before boosting.";
    }

    private async void DeviceOptimiseButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        if (_busyOperation)
        {
            StatusText = "Please wait for the current operation to finish.";
            return;
        }

        if (!_deviceOptimizationService.IsWindows11OrLater(out var osDescription))
        {
            MessageBox.Show(
                this,
                $"Device Optimise is built for Windows 11 only.\n\nDetected: {osDescription}",
                "Windows 11 Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!_isAdministrator)
        {
            var elevateChoice = MessageBox.Show(
                this,
                "Device Optimise needs administrator mode for the full Windows 11 gaming-session optimisation, including power plan and service control.\n\nRelaunch Mem-Booster as administrator now?",
                "Administrator Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (elevateChoice == MessageBoxResult.Yes)
            {
                RelaunchAsAdminAndExit();
            }
            else
            {
                StatusText = "Device Optimise cancelled. Administrator mode is required.";
            }

            return;
        }

        if (_deviceOptimizationService.HasActiveState())
        {
            MessageBox.Show(
                this,
                "Device Optimise is already active on this PC. Use Revert Device Optimise before applying it again.",
                "Device Optimise Already Active",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            UpdateDeviceOptimiseButtonState();
            return;
        }

        var selectionDialog = new DeviceOptimizationSelectionDialog(
            "Device Optimise",
            "Choose exactly which Windows 11 gaming-session optimisations you want. Recommended items are selected by default. Advanced or restart-required items show a warning. All listed items are captured before change and restored by Revert Device Optimise.\n\nMem-Booster will NOT disable antivirus/security, Memory Integrity/VBS, HPET, GPU drivers, network adapters, VPN/firewall tools, game launchers, anti-cheat, Discord, MSI Afterburner or RivaTuner.",
            _deviceOptimizationService.GetAvailableOptions(),
            "Apply Selected",
            15)
        {
            Owner = this
        };

        if (selectionDialog.ShowDialog() != true)
        {
            StatusText = "Device Optimise cancelled.";
            return;
        }

        var selectedDeviceOptionIds = selectionDialog.SelectedOptionIds.ToArray();
        if (selectedDeviceOptionIds.Length == 0)
        {
            StatusText = "Device Optimise cancelled. No options selected.";
            return;
        }

        var selectedDeviceOptions = _deviceOptimizationService.GetAvailableOptions()
            .Where(option => selectedDeviceOptionIds.Contains(option.Id, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        _loggerService.WriteDeviceOptimise("Device Optimise selected options: " + string.Join(", ", selectedDeviceOptions.Select(option => option.Name)));

        _processTimer.Stop();
        using var operation = _loggerService.BeginOperation("DeviceOptimise", $"version={CurrentVersion}; admin={_isAdministrator}");
        SetBusyState(true, "Device Optimise running...");
        ShowOperationProgress("Preparing Device Optimise...", 5);

        try
        {
            var result = await Task.Run(() => _deviceOptimizationService.Apply(
                selectedDeviceOptionIds,
                message =>
                {
                    _loggerService.WriteDeviceOptimise(message);
                    operation.Checkpoint(message);
                },
                (text, percent) => Dispatcher.Invoke(() => UpdateOperationProgress(text, percent))));

            UpdateDeviceOptimiseButtonState();
            UpdateOperationProgress("Refreshing after Device Optimise...", 92);
            UpdateMemoryInfo();
            await RefreshProcessesAsync(force: true, showProgress: true, progressContext: "Device Optimise refresh");
            UpdateOperationProgress("Device Optimise complete.", 100);
            StatusText = result.Summary + " Restart Windows to fully return to the normal background-app state.";
            ProfileStatus = result.Summary;

            if (!result.Success)
            {
                MessageBox.Show(this, result.Summary + "\n\nCheck Logs > device-optimise.log for details.", "Device Optimise Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Device Optimise Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "Device Optimise failed: " + ex.Message;
            _loggerService.WriteDeviceOptimise("Device Optimise failed: " + ex);
            _loggerService.Write("Device Optimise failed: " + ex);
        }
        finally
        {
            await HideOperationProgressAfterDelayAsync(600);
            SetBusyState(false);
            UpdateDeviceOptimiseButtonState();
            if (AutoRefreshCheckBox.IsChecked == true)
            {
                _processTimer.Start();
            }
        }
    }

    private async void RevertDeviceOptimiseButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        if (_busyOperation)
        {
            StatusText = "Please wait for the current operation to finish.";
            return;
        }

        if (!_deviceOptimizationService.HasActiveState())
        {
            MessageBox.Show(this, "No Device Optimise state was found on this PC.", "Nothing To Revert", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateDeviceOptimiseButtonState();
            return;
        }

        if (!_isAdministrator)
        {
            var elevateChoice = MessageBox.Show(
                this,
                "Revert Device Optimise needs administrator mode to restore the power plan and Windows Search service state.\n\nRelaunch Mem-Booster as administrator now?",
                "Administrator Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (elevateChoice == MessageBoxResult.Yes)
            {
                RelaunchAsAdminAndExit();
            }
            else
            {
                StatusText = "Revert Device Optimise cancelled. Administrator mode is required.";
            }

            return;
        }

        var appliedDeviceOptions = _deviceOptimizationService.GetAppliedOptions();
        var appliedText = appliedDeviceOptions.Count > 0
            ? string.Join("\n", appliedDeviceOptions.Select(option => "• " + option.Name + (option.RequiresRestart ? " (restart-related)" : string.Empty)))
            : "• Captured settings from an older Mem-Booster version";

        var confirm = ShowTimedWarning(
            "⚠ Revert Device Optimise Warning",
            "Mem-Booster will restore the Windows 11 settings captured before Device Optimise. Captured items:\n\n" +
            appliedText +
            "\n\nThis does not reopen apps closed by Boost Now. Restart Windows after a heavy boost to return to a clean normal background-app state, especially if you selected a restart-required optimisation.",
            "Revert Device Optimise",
            15);

        if (!confirm)
        {
            StatusText = "Revert Device Optimise cancelled.";
            return;
        }

        _processTimer.Stop();
        using var operation = _loggerService.BeginOperation("RevertDeviceOptimise", $"version={CurrentVersion}; admin={_isAdministrator}");
        SetBusyState(true, "Reverting Device Optimise...");
        ShowOperationProgress("Preparing Device Optimise revert...", 5);

        try
        {
            var result = await Task.Run(() => _deviceOptimizationService.Revert(
                message =>
                {
                    _loggerService.WriteDeviceOptimise(message);
                    operation.Checkpoint(message);
                },
                (text, percent) => Dispatcher.Invoke(() => UpdateOperationProgress(text, percent))));

            UpdateDeviceOptimiseButtonState();
            UpdateOperationProgress("Refreshing after revert...", 92);
            UpdateMemoryInfo();
            await RefreshProcessesAsync(force: true, showProgress: true, progressContext: "Device Optimise revert refresh");
            UpdateOperationProgress("Device Optimise revert complete.", 100);
            StatusText = result.Summary;
            ProfileStatus = result.Summary;

            if (!result.Success)
            {
                MessageBox.Show(this, result.Summary + "\n\nCheck Logs > device-optimise.log for details.", "Revert Device Optimise Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Revert Device Optimise Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "Revert Device Optimise failed: " + ex.Message;
            _loggerService.WriteDeviceOptimise("Revert Device Optimise failed: " + ex);
            _loggerService.Write("Revert Device Optimise failed: " + ex);
        }
        finally
        {
            await HideOperationProgressAfterDelayAsync(600);
            SetBusyState(false);
            UpdateDeviceOptimiseButtonState();
            if (AutoRefreshCheckBox.IsChecked == true)
            {
                _processTimer.Start();
            }
        }
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        _selectedProcessNames.Clear();
        ApplySelectionToRunningRows();
        _loggerService.Write("Selection cleared by user.");
        ProfileStatus = "Selection cleared.";
        StatusText = "Profile cleared.";
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        ShowBoostPreview();
    }

    private void ShowBoostPreview()
    {
        if (_selectedProcessNames.Count == 0)
        {
            MessageBox.Show(this, "No apps selected. Select apps or load an XML profile first.", "Boost Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var runningMatches = ProcessGroups
            .Where(p => p.IsSelected)
            .OrderByDescending(p => p.WorkingSetBytes)
            .ToList();

        var notRunning = _selectedProcessNames
            .Where(name => runningMatches.All(p => !p.ExeName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var estimatedMemory = FormatBytes(runningMatches.Sum(p => p.WorkingSetBytes));
        var runningLines = runningMatches.Count == 0
            ? "No selected profile apps are running right now."
            : string.Join(Environment.NewLine, runningMatches.Take(18).Select(p => $"• {p.DisplayName} ({p.ExeName}) - {p.MemoryText}, {p.InstanceCount} instance(s)"));

        var skippedText = notRunning.Count == 0
            ? "None"
            : string.Join(", ", notRunning.Take(20)) + (notRunning.Count > 20 ? "..." : string.Empty);

        MessageBox.Show(
            this,
            $"Running matches to close: {runningMatches.Count}\nEstimated RAM currently used by matches: {estimatedMemory}\n\n{runningLines}\n\nProfile entries not running / skipped:\n{skippedText}\n\nTip: restart Windows after a heavy boost to return to a clean normal background-app state.",
            "Boost Preview",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void BoostButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        if (_busyOperation)
        {
            StatusText = "Another operation is already running.";
            return;
        }

        if (_selectedProcessNames.Count == 0)
        {
            MessageBox.Show(this, "No apps selected. Select apps or load an XML profile first.", "Boost", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_isAdministrator)
        {
            var elevateChoice = MessageBox.Show(
                this,
                "Mem-Booster is running in standard mode. It can close normal user apps, but apps running as administrator or protected by higher privileges may reject force close.\n\nRelaunch Mem-Booster as administrator for best results?",
                "Administrator Recommended",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (elevateChoice == MessageBoxResult.Cancel)
            {
                StatusText = "Boost cancelled.";
                return;
            }

            if (elevateChoice == MessageBoxResult.Yes)
            {
                RelaunchAsAdminAndExit();
                return;
            }
        }

        var activeMatches = ProcessGroups.Count(p => p.IsSelected);
        var activeMemory = FormatBytes(ProcessGroups.Where(p => p.IsSelected).Sum(p => p.WorkingSetBytes));
        _loggerService.Write($"Boost confirmation shown; selected={_selectedProcessNames.Count}; activeMatches={activeMatches}; activeMemory={activeMemory}; admin={_isAdministrator}");
        var confirm = ShowTimedWarning(
            "⚠ Boost Now Warning",
            $"Warning: Mem-Booster will close the selected apps and their child/helper processes. This can close unsaved work and may temporarily break some Windows/app functionality.\n\nProfile entries: {_selectedProcessNames.Count}\nRunning matches: {activeMatches}\nEstimated RAM in matched apps: {activeMemory}\nSmart close first: {(SmartCloseCheckBox.IsChecked == true ? "On" : "Off")}\n\nMem-Booster will not try to reopen apps automatically. Restart Windows after a heavy boost if you want everything back to the normal background-app state. Use Revert Device Optimise if you changed Windows settings.",
            "Start Boost",
            5);

        if (!confirm)
        {
            StatusText = "Boost cancelled.";
            return;
        }

        _processTimer.Stop();
        using var operation = _loggerService.BeginOperation("BoostNow", $"selected={_selectedProcessNames.Count}; activeMatches={activeMatches}; admin={_isAdministrator}");
        SetBusyState(true, "Boost running... closing selected process trees.");
        ShowOperationProgress("Preparing boost...", 5);

        try
        {
            _loggerService.WriteProcessSnapshot("before-boost", ProcessGroups, _selectedProcessNames);
            UpdateOperationProgress("Snapshot saved. Closing selected processes...", 24);
            operation.Checkpoint("Before-boost snapshot saved");
            var options = new TerminationOptions(SmartCloseCheckBox.IsChecked == true, 750, 1200);
            var selectedCsv = string.Join(", ", _selectedProcessNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

            var result = await Task.Run(() => _processService.KillProcessTreesByExecutableNames(_selectedProcessNames, options, message => operation.Checkpoint(message)));
            _loggerService.WriteBoost(selectedCsv, result);
            UpdateOperationProgress("Processes closed. Refreshing memory and app list...", 78);

            UpdateMemoryInfo();
            operation.Checkpoint("Memory refreshed after boost");
            await RefreshProcessesAsync(force: true, showProgress: true, progressContext: "Boost refresh");
            _loggerService.WriteProcessSnapshot("after-boost", ProcessGroups, _selectedProcessNames);
            UpdateOperationProgress("Boost complete.", 100);
            operation.Checkpoint("After-boost refresh and snapshot complete");

            var details = result.Messages.Count == 0
                ? string.Empty
                : "\n\nDetails:\n" + string.Join("\n", result.Messages.Take(8));

            StatusText = result.Summary + " Restart Windows to fully return to the normal background-app state.";
            if (result.Failed > 0)
            {
                MessageBox.Show(this, result.Summary + details, "Boost Completed With Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                _loggerService.Write($"Boost complete notification kept in status bar: {result.Summary}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Boost Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = $"Boost failed: {ex.Message}";
            _loggerService.Write($"Boost failed: {ex}");
        }
        finally
        {
            await HideOperationProgressAfterDelayAsync(500);
            SetBusyState(false);
            if (AutoRefreshCheckBox.IsChecked == true)
            {
                _processTimer.Start();
            }
        }
    }


    private void SelectionCheckBox_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is CheckBox checkBox && checkBox.Tag is string exeName)
        {
            _loggerService.Write($"Selection checkbox clicked: exe={exeName}; checked={checkBox.IsChecked}");
        }
    }

    private void SelectAppNameButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        e.Handled = true;

        if (_busyOperation)
        {
            StatusText = "Please wait for the current operation to finish.";
            return;
        }

        if (sender is not Button button || button.Tag is not string exeName || string.IsNullOrWhiteSpace(exeName))
        {
            _loggerService.Write("App-name select ignored: missing executable tag.");
            return;
        }

        var process = ProcessGroups.FirstOrDefault(p => p.ExeName.Equals(exeName, StringComparison.OrdinalIgnoreCase));
        if (process is null)
        {
            _loggerService.Write($"App-name select ignored: process disappeared before click was processed | exe={exeName}");
            StatusText = "That app is no longer running. Refresh the list and try again.";
            return;
        }

        SelectProcess(process, "app-name-click");
    }

    private void SelectProcess(ProcessGroup process, string source)
    {
        if (!process.CanSelect)
        {
            StatusText = $"{process.DisplayName} is protected and cannot be selected.";
            _loggerService.Write($"Manual select blocked ({source}): {process.DisplayName} | {process.ExeName} | reason=protected");
            return;
        }

        if (process.IsSelected)
        {
            StatusText = $"{process.DisplayName} is already selected. Use the checkbox or selected-apps panel to remove it.";
            return;
        }

        process.IsSelected = true;
        _loggerService.Write($"Manual select ({source}): {process.DisplayName} | {process.ExeName} | selected=True");
    }

    private void SuppressRowToggle(int milliseconds = 350)
    {
        _suppressRowToggleUntilUtc = DateTime.UtcNow.AddMilliseconds(milliseconds);
    }

    private void SetBusyState(bool busy, string? status = null)
    {
        _busyOperation = busy;
        SuppressRowToggle(busy ? 1500 : 500);
        Cursor = busy ? Cursors.Wait : null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            StatusText = status;
        }
    }

    private void RemoveSelectedApp_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        if (sender is not Button button || button.Tag is not string exeName)
        {
            return;
        }

        _selectedProcessNames.Remove(exeName);
        ApplySelectionToRunningRows();
        _loggerService.Write($"Selected panel remove: {exeName}");
        StatusText = $"Removed {ToFriendlyFallbackName(exeName)} from the selected profile.";
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text.Trim();
        _processView.Refresh();
        _loggerService.Write($"Search filter changed: length={_searchText.Length}");
    }

    private void AutoRefreshCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_processTimer is null)
        {
            return;
        }

        if (AutoRefreshCheckBox.IsChecked == true)
        {
            if (!_busyOperation)
            {
                _processTimer.Start();
            }
            StatusText = "Auto-refresh enabled.";
        }
        else
        {
            _processTimer.Stop();
            StatusText = "Auto-refresh paused. Use Refresh manually.";
        }
    }

    private void RunAsAdminButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        RelaunchAsAdminAndExit();
    }

    private void RelaunchAsAdminAndExit()
    {
        if (_isAdministrator)
        {
            MessageBox.Show(this, "Mem-Booster is already running as administrator.", "Administrator Mode", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _profileService.SaveLocalProfile(GetSaveableProfileNames());
            if (AdminService.RelaunchAsAdministrator())
            {
                Application.Current.Shutdown();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Administrator relaunch cancelled.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Run as Admin Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "Administrator relaunch failed.";
        }
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        if (_lastUpdateCheck?.IsUpdateAvailable == true && !string.IsNullOrWhiteSpace(_lastUpdateCheck.ReleaseUrl))
        {
            var openChoice = MessageBox.Show(
                this,
                $"A newer Mem-Booster release is available.\n\nCurrent: v{CurrentVersion}\nLatest: {_lastUpdateCheck.LatestVersion}\n\nOpen the GitHub release page?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (openChoice == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(_lastUpdateCheck.ReleaseUrl) { UseShellExecute = true });
            }

            return;
        }

        await CheckForUpdatesAsync(silent: false);
    }

    private async Task CheckForUpdatesAsync(bool silent)
    {
        if (_checkingForUpdates)
        {
            return;
        }

        _checkingForUpdates = true;
        using var operation = _loggerService.BeginOperation("CheckUpdates", $"silent={silent}; current={CurrentVersion}");
        var previousButtonText = UpdateButtonText;
        UpdateButtonText = "Checking...";

        try
        {
            var result = await _updateService.CheckLatestAsync(CurrentVersion);
            _lastUpdateCheck = result;
            operation.Checkpoint($"latest={result.LatestVersion}; updateAvailable={result.IsUpdateAvailable}; releaseUrl={result.ReleaseUrl}");

            if (result.IsUpdateAvailable)
            {
                UpdateButtonText = $"Update {result.LatestVersion}";
                StatusText = $"Update available: {result.LatestVersion}";

                if (!silent)
                {
                    var openChoice = MessageBox.Show(
                        this,
                        $"A newer Mem-Booster release is available.\n\nCurrent: v{CurrentVersion}\nLatest: {result.LatestVersion}\n\nOpen the GitHub release page?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (openChoice == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(result.ReleaseUrl))
                    {
                        Process.Start(new ProcessStartInfo(result.ReleaseUrl) { UseShellExecute = true });
                    }
                }
            }
            else
            {
                UpdateButtonText = "Up to date";
                var latestDisplay = string.IsNullOrWhiteSpace(result.LatestVersion) ? "No published release found" : $"v{result.LatestVersion}";
                StatusText = $"Up to date. GitHub latest release: {latestDisplay}.";
                if (!silent)
                {
                    MessageBox.Show(this, $"Mem-Booster is up to date.\n\nInstalled: v{CurrentVersion}\nGitHub latest release: {latestDisplay}", "Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            UpdateButtonText = previousButtonText == "Checking..." ? "Updates" : previousButtonText;
            _loggerService.Write($"Update check failed: {ex}");

            if (!silent)
            {
                MessageBox.Show(this, "Could not check for updates. Please check your internet connection and try again.\n\n" + ex.Message, "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            _checkingForUpdates = false;
        }
    }

    private void CollectDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        try
        {
            using var operation = _loggerService.BeginOperation("CollectDiagnostics", $"version={CurrentVersion}");
            var zipPath = _diagnosticsService.Collect(_profileService.AppDataDirectory, CurrentVersion);
            operation.Checkpoint($"Diagnostics zip={zipPath}");
            StatusText = "Diagnostics package created.";

            var openChoice = MessageBox.Show(
                this,
                $"Diagnostics package created:\n\n{zipPath}\n\nOpen the folder now?",
                "Diagnostics Collected",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (openChoice == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zipPath}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Diagnostics Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "Diagnostics collection failed.";
            _loggerService.Write($"Diagnostics failed: {ex}");
        }
    }


    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        try
        {
            var logsDirectory = Path.Combine(_profileService.AppDataDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);
            _loggerService.Write("Logs folder opened by user.");
            Process.Start(new ProcessStartInfo("explorer.exe", logsDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open logs", MessageBoxButton.OK, MessageBoxImage.Error);
            _loggerService.Write($"Open logs failed: {ex}");
        }
    }

    private void OpenGithub_Click(object sender, RoutedEventArgs e)
    {
        SuppressRowToggle();
        try
        {
            Process.Start(new ProcessStartInfo(UpdateService.RepositoryUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open GitHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplySelectionToRunningRows()
    {
        _syncingSelection = true;
        try
        {
            foreach (var process in ProcessGroups)
            {
                process.IsSelected = process.CanSelect && _selectedProcessNames.Contains(process.ExeName);
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        UpdateSelectionSummary();
    }

    private static string FormatBytes(long bytes)
    {
        var mb = bytes / 1024d / 1024d;
        return mb >= 1024
            ? $"{mb / 1024d:0.0} GB"
            : $"{mb:0} MB";
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        try
        {
            _memoryTimer?.Stop();
            _processTimer?.Stop();
            _progressTimer?.Stop();
        }
        catch
        {
            // Ignore timer shutdown issues during app close.
        }

        try
        {
            _profileService.SaveLocalProfile(_selectedProcessNames);
        }
        catch
        {
            // Do not block app close because profile auto-save failed.
        }

        base.OnClosing(e);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
