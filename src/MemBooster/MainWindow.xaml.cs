using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using MemBooster.Models;
using MemBooster.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.System;
using WinRT.Interop;

namespace MemBooster;

public sealed partial class MainWindow : Window
{
    private const string CurrentVersion = "0.6.14";
    private const string RepositoryUrl = "https://github.com/ox1d3x3/mem-booster";

    private readonly MemoryService _memoryService = new();
    private readonly ProcessService _processService = new();
    private readonly ProfileService _profileService;
    private readonly LoggerService _loggerService;
    private readonly UpdateService _updateService = new();
    private readonly DiagnosticsService _diagnosticsService;
    private readonly DeviceOptimizationService _deviceOptimizationService;

    private readonly HashSet<string> _selectedProcessNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProcessGroup> _allProcessGroups = new();
    private readonly Stopwatch _operationWatch = new();
    private readonly string _appDataDirectory;

    private bool _syncingSelection;
    private bool _busyOperation;
    private bool _isDarkTheme = true;
    private bool _themeAnimationRunning;
    private string _searchText = string.Empty;
    private string? _pendingReleaseUrl;

    public ObservableCollection<ProcessGroup> ProcessGroups { get; } = new();
    public ObservableCollection<SelectedAppItem> SelectedApps { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _profileService = new ProfileService();
        _appDataDirectory = _profileService.AppDataDirectory;
        Directory.CreateDirectory(_appDataDirectory);

        _loggerService = new LoggerService(_appDataDirectory);
        _diagnosticsService = new DiagnosticsService();
        _deviceOptimizationService = new DeviceOptimizationService(_appDataDirectory);

        ProcessesList.ItemsSource = ProcessGroups;
        SelectedList.ItemsSource = SelectedApps;

        ApplyTheme(_profileService.LoadThemePreference(), animate: false);
        ApplyWindowIcon();
        ApplyAdminState();

        var localProfile = _profileService.LoadLocalProfile();
        foreach (var name in localProfile.ExecutableNames)
        {
            _selectedProcessNames.Add(SafetyRules.NormaliseProcessName(name));
        }

        ProfileStatusTextBlock.Text = _selectedProcessNames.Count > 0
            ? $"Loaded local profile with {_selectedProcessNames.Count} app(s)."
            : "No saved profile loaded.";

        _profileService.ClearRestoreSession();
        RevertDeviceOptimiseButton.IsEnabled = _deviceOptimizationService.HasActiveState();

        RootGrid.Loaded += async (_, _) =>
        {
            await RefreshProcessesAsync("Initial load", true);
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                await CheckUpdatesAsync(silent: true);
            });
        };
    }

    private void ApplyAdminState()
    {
        var elevated = AdminService.IsCurrentProcessElevated();
        AdminModeTextBlock.Text = elevated ? "Administrator mode" : "Standard mode";
        RunAsAdminButton.IsEnabled = !elevated;
    }

    private void ApplyTheme(string theme, bool animate = false)
    {
        _isDarkTheme = !string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        var requestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light;
        RootBorder.RequestedTheme = requestedTheme;
        RootGrid.RequestedTheme = requestedTheme;
        ToolTipService.SetToolTip(ThemeToggleButton, _isDarkTheme ? "Switch to light theme" : "Switch to dark theme");

        if (animate)
        {
            _ = AnimateThemeToggleAsync(_isDarkTheme);
        }
        else
        {
            SetThemeToggleVisual(_isDarkTheme);
        }
    }

    private void SetThemeToggleVisual(bool dark)
    {
        ThemeThumbTranslate.X = dark ? 0 : 20;
        ThemeIconText.Text = dark ? "☾" : "☀";
        ThemeLabelText.Text = dark ? "Dark" : "Light";
        ThemeIconText.Opacity = 1;
        ThemeThumb.Opacity = 1;
    }

    private async Task AnimateThemeToggleAsync(bool dark)
    {
        if (_themeAnimationRunning)
        {
            SetThemeToggleVisual(dark);
            return;
        }

        _themeAnimationRunning = true;

        try
        {
            var start = ThemeThumbTranslate.X;
            var end = dark ? 0d : 20d;
            var iconChanged = false;

            for (var frame = 1; frame <= 16; frame++)
            {
                var t = frame / 16d;
                var eased = 1d - Math.Pow(1d - t, 3d);
                ThemeThumbTranslate.X = start + ((end - start) * eased);

                if (!iconChanged && t >= 0.45d)
                {
                    ThemeIconText.Text = dark ? "☾" : "☀";
                    ThemeLabelText.Text = dark ? "Dark" : "Light";
                    iconChanged = true;
                }

                var distanceFromMiddle = Math.Abs(t - 0.5d) * 2d;
                ThemeIconText.Opacity = 0.45d + (0.55d * distanceFromMiddle);
                await Task.Delay(10);
            }

            SetThemeToggleVisual(dark);
        }
        finally
        {
            _themeAnimationRunning = false;
        }
    }

    private void ApplyWindowIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "mem-booster.ico");
            if (!File.Exists(iconPath))
            {
                _loggerService.Write($"Window icon missing: {iconPath}");
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(iconPath);
            _loggerService.Write($"Window icon applied: {iconPath}");
        }
        catch (Exception ex)
        {
            _loggerService.Write($"Failed to apply window icon: {ex}");
        }
    }

    private async Task RefreshProcessesAsync(string context, bool showProgress)
    {
        if (_busyOperation)
        {
            return;
        }

        _busyOperation = true;
        var watch = Stopwatch.StartNew();
        try
        {
            if (showProgress)
            {
                ShowProgress($"{context}: scanning running apps...", 15);
            }

            RefreshMemory();

            var snapshots = await Task.Run(() => _processService.GetProcessGroups());
            var ordered = snapshots
                .Select(s =>
                {
                    var group = new ProcessGroup(s.ExeName);
                    group.UpdateFrom(s, _selectedProcessNames.Contains(s.ExeName));
                    group.PropertyChanged += ProcessGroup_PropertyChanged;
                    return group;
                })
                .OrderByDescending(g => g.WorkingSetBytes)
                .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _syncingSelection = true;
            try
            {
                foreach (var group in _allProcessGroups)
                {
                    group.PropertyChanged -= ProcessGroup_PropertyChanged;
                }

                _allProcessGroups.Clear();
                _allProcessGroups.AddRange(ordered);
            }
            finally
            {
                _syncingSelection = false;
            }

            ApplyFilter();
            UpdateSelectionSummary();

            watch.Stop();
            StatusTextBlock.Text = $"Running apps refreshed in {watch.ElapsedMilliseconds} ms.";
            if (!string.Equals(context, "Manual refresh", StringComparison.OrdinalIgnoreCase) || watch.ElapsedMilliseconds >= 50)
            {
                _loggerService.Write($"WinUI refresh complete: context={context}; groups={_allProcessGroups.Count}; visible={ProcessGroups.Count}; selected={_selectedProcessNames.Count}; elapsedMs={watch.ElapsedMilliseconds}");
            }

            if (showProgress)
            {
                ShowProgress($"Loaded {ProcessGroups.Count} app group(s)", 100);
                await Task.Delay(300);
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Refresh failed: {ex.Message}";
            _loggerService.Write($"WinUI refresh failed: {ex}");
            await ShowMessageAsync("Refresh failed", ex.Message);
        }
        finally
        {
            HideProgress();
            _busyOperation = false;
        }
    }

    private void ApplyFilter()
    {
        var query = _searchText.Trim();

        ProcessGroups.Clear();

        foreach (var group in _allProcessGroups)
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                var match = group.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || group.ExeName.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || group.RiskLabel.Contains(query, StringComparison.OrdinalIgnoreCase);

                if (!match)
                {
                    continue;
                }
            }

            ProcessGroups.Add(group);
        }

        ProcessCountTextBlock.Text = _allProcessGroups.Count.ToString();
    }

    private void RefreshMemory()
    {
        var info = _memoryService.GetMemoryInfo();
        MemoryTextBlock.Text = info.Summary;
        MemoryBar.Value = info.UsedPercent;
    }

    private void ProcessGroup_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncingSelection || e.PropertyName != nameof(ProcessGroup.IsSelected) || sender is not ProcessGroup group)
        {
            return;
        }

        if (group.IsSelected)
        {
            _selectedProcessNames.Add(group.ExeName);
        }
        else
        {
            _selectedProcessNames.Remove(group.ExeName);
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        SelectedApps.Clear();

        var selectedGroups = _allProcessGroups
            .Where(g => _selectedProcessNames.Contains(g.ExeName))
            .OrderByDescending(g => g.WorkingSetBytes)
            .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in selectedGroups)
        {
            SelectedApps.Add(new SelectedAppItem(
                group.DisplayName,
                group.ExeName,
                $"{group.ExeName} • {group.MemoryText} • {group.InstanceCount} instance(s)"));
        }

        var active = selectedGroups.Count;
        var total = _selectedProcessNames.Count;
        var text = total == 0 ? "0 selected" : $"{active} active / {total} apps";
        SelectedTextBlock.Text = text;
        SelectedPanelTextBlock.Text = text;

        _syncingSelection = true;
        try
        {
            foreach (var group in _allProcessGroups)
            {
                var shouldBeSelected = _selectedProcessNames.Contains(group.ExeName) && group.CanSelect;
                if (group.IsSelected != shouldBeSelected)
                {
                    group.IsSelected = shouldBeSelected;
                }
            }
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void SetSelection(IEnumerable<string> executableNames, string mode)
    {
        _selectedProcessNames.Clear();

        foreach (var exe in executableNames)
        {
            var name = SafetyRules.NormaliseProcessName(exe);
            if (!string.IsNullOrWhiteSpace(name) && SafetyRules.IsAutoLoadProfileAllowed(name))
            {
                _selectedProcessNames.Add(name);
            }
        }

        UpdateSelectionSummary();
        ApplyFilter();

        var running = _allProcessGroups.Count(g => _selectedProcessNames.Contains(g.ExeName));
        ProfileStatusTextBlock.Text = $"{mode}: {_selectedProcessNames.Count} selected, {running} currently running.";
        _loggerService.WriteSelection($"WinUI {mode}", _selectedProcessNames, _allProcessGroups);
    }

    private void ToggleProcess(ProcessGroup group)
    {
        if (_busyOperation)
        {
            StatusTextBlock.Text = "Please wait for the current operation to finish.";
            return;
        }

        if (!group.CanSelect)
        {
            StatusTextBlock.Text = $"{group.DisplayName} is protected and cannot be selected.";
            return;
        }

        group.IsSelected = !group.IsSelected;
        StatusTextBlock.Text = group.IsSelected ? $"Selected {group.DisplayName}." : $"Removed {group.DisplayName}.";
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshProcessesAsync("Manual refresh", true);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        ApplyFilter();
    }

    private void ProcessCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProcessGroup group })
        {
            ToggleProcess(group);
        }
    }

    private void ProcessRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProcessGroup group })
        {
            ToggleProcess(group);
        }
    }

    private void RemoveSelectedApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SelectedAppItem item })
        {
            _selectedProcessNames.Remove(item.ExeName);
            UpdateSelectionSummary();
            ApplyFilter();
            StatusTextBlock.Text = $"Removed {item.DisplayName}.";
        }
    }

    private void SelectRecommendedButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelection(_allProcessGroups
            .Where(p => p.CanSelect && SafetyRules.IsRecommendedForGamingBoost(p.ExeName))
            .Select(p => p.ExeName), "Safe Select");
    }

    private void ExtremeSelectButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelection(_allProcessGroups
            .Where(p => p.CanSelect && SafetyRules.IsExtremeRecommendedForGamingBoost(p.ExeName))
            .Select(p => p.ExeName), "Extreme Select");
    }

    private void AggressiveSelectButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelection(_allProcessGroups
            .Where(p => p.CanSelect && SafetyRules.IsAggressiveRecommendedForGamingBoost(p.ExeName))
            .Select(p => p.ExeName), "Aggressive Select");
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedProcessNames.Clear();
        UpdateSelectionSummary();
        ApplyFilter();
        ProfileStatusTextBlock.Text = "Selection cleared.";
        StatusTextBlock.Text = "Selected profile cleared.";
    }

    private async void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        var running = _allProcessGroups
            .Where(p => _selectedProcessNames.Contains(p.ExeName))
            .OrderByDescending(p => p.WorkingSetBytes)
            .ToList();

        var body = running.Count == 0
            ? "No selected apps are currently running."
            : string.Join(Environment.NewLine, running.Take(40).Select(p => $"• {p.DisplayName} ({p.ExeName}) - {p.MemoryText}"));

        await ShowMessageAsync("Preview Boost", body);
    }

    private async void BoostButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busyOperation)
        {
            return;
        }

        var runningTargets = _allProcessGroups
            .Where(p => _selectedProcessNames.Contains(p.ExeName))
            .ToList();

        if (runningTargets.Count == 0)
        {
            await ShowMessageAsync("Nothing to boost", "No selected apps are currently running.");
            return;
        }

        if (!AdminService.IsCurrentProcessElevated())
        {
            var adminChoice = await ShowConfirmAsync(
                "Run as administrator?",
                "Mem-Booster is currently in Standard mode. Some elevated apps may refuse to close. Relaunch as administrator now?",
                "Relaunch",
                "Continue");
            if (adminChoice)
            {
                try
                {
                    _profileService.SaveLocalProfile(_selectedProcessNames);
                    if (AdminService.RelaunchAsAdministrator())
                    {
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync("Admin relaunch failed", ex.Message);
                }

                return;
            }
        }

        var selectedMemory = runningTargets.Sum(p => p.WorkingSetBytes);
        var warning = $"Mem-Booster will close {runningTargets.Count} selected running app group(s).{Environment.NewLine}{Environment.NewLine}" +
                      $"Estimated selected working set: {FormatBytes(selectedMemory)}{Environment.NewLine}{Environment.NewLine}" +
                      "Save your work first. Heavy boost may stop background sync, helpers, web views, editors or work apps. " +
                      "Restart Windows after gaming if you want the normal background-app state back.";

        var accepted = await ShowTimedWarningAsync("Boost Now Warning", warning, 5);
        if (!accepted)
        {
            StatusTextBlock.Text = "Boost cancelled.";
            return;
        }

        _busyOperation = true;
        _operationWatch.Restart();

        try
        {
            ShowProgress("Preparing boost...", 10);
            var before = _loggerService.WriteProcessSnapshot("before-boost", _allProcessGroups, _selectedProcessNames);
            _loggerService.Write($"WinUI before-boost snapshot: {before}");

            var fastBoost = FastBoostCheckBox.IsChecked == true;
            var smartClose = SmartCloseCheckBox.IsChecked == true && !fastBoost;
            var options = fastBoost
                ? new TerminationOptions(false, 0, 900)
                : new TerminationOptions(smartClose, 750, 1200);

            ShowProgress("Closing selected app process trees...", 45);

            var result = await Task.Run(() => _processService.KillProcessTreesByExecutableNames(
                _selectedProcessNames,
                options,
                line => _loggerService.WritePerformance($"WinUI boost: {line}")));

            _loggerService.WriteBoost(string.Join(", ", _selectedProcessNames.OrderBy(n => n)), result);

            ShowProgress("Refreshing memory and running app list...", 80);
            await RefreshProcessesAfterBoostAsync();

            ShowProgress("Boost complete.", 100);
            StatusTextBlock.Text = $"Boost complete: {result.Summary}. Restart Windows to return to normal background-app state.";
            ElapsedTextBlock.Text = $"{_operationWatch.Elapsed.TotalSeconds:0.0}s";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Boost failed: {ex.Message}";
            _loggerService.Write($"WinUI boost failed: {ex}");
            await ShowMessageAsync("Boost failed", ex.Message);
        }
        finally
        {
            _operationWatch.Stop();
            await Task.Delay(350);
            HideProgress();
            _busyOperation = false;
        }
    }

    private async Task RefreshProcessesAfterBoostAsync()
    {
        RefreshMemory();

        var snapshots = await Task.Run(() => _processService.GetProcessGroups());
        var incoming = snapshots.Select(s => s.ExeName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var oldGroup in _allProcessGroups)
        {
            oldGroup.PropertyChanged -= ProcessGroup_PropertyChanged;
        }

        _allProcessGroups.Clear();

        foreach (var snapshot in snapshots.OrderByDescending(s => s.WorkingSetBytes))
        {
            var group = new ProcessGroup(snapshot.ExeName);
            group.UpdateFrom(snapshot, _selectedProcessNames.Contains(snapshot.ExeName));
            group.PropertyChanged += ProcessGroup_PropertyChanged;
            _allProcessGroups.Add(group);
        }

        ApplyFilter();
        UpdateSelectionSummary();
        _loggerService.WriteProcessSnapshot("after-boost", _allProcessGroups, _selectedProcessNames);
    }

    private void SaveLocalButton_Click(object sender, RoutedEventArgs e)
    {
        _profileService.SaveLocalProfile(_selectedProcessNames);
        ProfileStatusTextBlock.Text = $"Local profile saved with {_selectedProcessNames.Count} app(s).";
        StatusTextBlock.Text = "Local profile saved.";
    }

    private async void ExportXmlButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePath = await PickSaveProfilePathAsync();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _profileService.SaveProfile(filePath, new ProfileData("Gaming Boost", _selectedProcessNames));
            StatusTextBlock.Text = $"Profile exported: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            _loggerService.Write($"WinUI export profile failed: {ex}");
            await ShowMessageAsync("Export failed", ToUserError(ex));
        }
    }

    private async void LoadXmlButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePath = await PickOpenProfilePathAsync();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var profile = _profileService.LoadProfile(filePath);
            SetSelection(profile.ExecutableNames, $"Loaded XML: {profile.Name}");
            StatusTextBlock.Text = $"Loaded profile: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            _loggerService.Write($"WinUI load profile failed: {ex}");
            await ShowMessageAsync("Load failed", ToUserError(ex));
        }
    }


    private Task<string?> PickOpenProfilePathAsync()
    {
        try
        {
            var path = NativeFileDialogService.ShowOpenXmlProfileDialog(WindowNative.GetWindowHandle(this));
            return Task.FromResult(path);
        }
        catch (Exception ex)
        {
            _loggerService.Write($"Explorer profile open dialog failed: {ex}");
            throw new InvalidOperationException("The Explorer file picker could not open. " + ToUserError(ex), ex);
        }
    }

    private Task<string?> PickSaveProfilePathAsync()
    {
        try
        {
            var path = NativeFileDialogService.ShowSaveXmlProfileDialog(WindowNative.GetWindowHandle(this));
            return Task.FromResult(path);
        }
        catch (Exception ex)
        {
            _loggerService.Write($"Explorer profile save dialog failed: {ex}");
            throw new InvalidOperationException("The Explorer file picker could not open. " + ToUserError(ex), ex);
        }
    }

    private static string ToUserError(Exception ex)
    {
        if (!string.IsNullOrWhiteSpace(ex.Message))
        {
            return ex.Message;
        }

        return ex.ToString();
    }

    private async void DeviceOptimiseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AdminService.IsCurrentProcessElevated())
        {
            var relaunch = await ShowConfirmAsync(
                "Administrator mode recommended",
                "Device Optimise changes Windows settings and should be run as administrator.",
                "Relaunch",
                "Cancel");

            if (relaunch)
            {
                try
                {
                    if (AdminService.RelaunchAsAdministrator())
                    {
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync("Admin relaunch failed", ex.Message);
                }
            }

            return;
        }

        if (!_deviceOptimizationService.IsWindows11OrLater(out var osDescription))
        {
            await ShowMessageAsync("Windows 11 required", $"Device Optimise is focused on Windows 11. Detected: {osDescription}");
            return;
        }

        var options = _deviceOptimizationService.GetAvailableOptions();
        var selectedIds = await ShowDeviceOptionsDialogAsync(options);
        if (selectedIds is null || selectedIds.Count == 0)
        {
            StatusTextBlock.Text = "Device Optimise cancelled.";
            return;
        }

        var accepted = await ShowTimedWarningAsync(
            "Device Optimise Warning",
            "Selected Windows 11 gaming-session settings will be applied. Save work first. Use Revert Device Optimise to restore captured settings.",
            15);

        if (!accepted)
        {
            return;
        }

        _busyOperation = true;
        try
        {
            ShowProgress("Applying Device Optimise...", 20);
            var result = await Task.Run(() => _deviceOptimizationService.Apply(
                selectedIds,
                line => _loggerService.WriteDeviceOptimise(line),
                (message, value) => DispatcherQueue.TryEnqueue(() => ShowProgress(message, value))));

            RevertDeviceOptimiseButton.IsEnabled = _deviceOptimizationService.HasActiveState();
            StatusTextBlock.Text = result.Summary;
            await ShowMessageAsync("Device Optimise", result.Summary);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Device Optimise failed", ex.Message);
            _loggerService.WriteDeviceOptimise("Apply failed: " + ex);
        }
        finally
        {
            HideProgress();
            _busyOperation = false;
        }
    }

    private async void RevertDeviceOptimiseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_deviceOptimizationService.HasActiveState())
        {
            await ShowMessageAsync("Nothing to revert", "No Device Optimise state is currently captured.");
            return;
        }

        var accepted = await ShowTimedWarningAsync(
            "Revert Device Optimise",
            "Captured Windows setting values will be restored. Some changes may still require a restart to fully apply.",
            15);

        if (!accepted)
        {
            return;
        }

        _busyOperation = true;
        try
        {
            ShowProgress("Reverting Device Optimise...", 30);
            var result = await Task.Run(() => _deviceOptimizationService.Revert(
                line => _loggerService.WriteDeviceOptimise(line),
                (message, value) => DispatcherQueue.TryEnqueue(() => ShowProgress(message, value))));

            RevertDeviceOptimiseButton.IsEnabled = _deviceOptimizationService.HasActiveState();
            StatusTextBlock.Text = result.Summary;
            await ShowMessageAsync("Revert Device Optimise", result.Summary);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Revert failed", ex.Message);
            _loggerService.WriteDeviceOptimise("Revert failed: " + ex);
        }
        finally
        {
            HideProgress();
            _busyOperation = false;
        }
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckUpdatesAsync(silent: false);
    }

    private async Task CheckUpdatesAsync(bool silent)
    {
        try
        {
            var result = await _updateService.CheckLatestAsync(CurrentVersion);
            _pendingReleaseUrl = result.ReleaseUrl;

            if (result.IsUpdateAvailable)
            {
                UpdateButton.Content = $"Update {result.LatestVersion}";
                if (!silent)
                {
                    var open = await ShowConfirmAsync("Update available", $"Latest release: {result.LatestVersion}", "Open GitHub", "Close");
                    if (open)
                    {
                        await Launcher.LaunchUriAsync(new Uri(result.ReleaseUrl));
                    }
                }
            }
            else
            {
                UpdateButton.Content = "Up to date";
                if (!silent)
                {
                    await ShowMessageAsync("Updates", string.IsNullOrWhiteSpace(result.LatestVersion)
                        ? "No GitHub release was found yet."
                        : $"Latest GitHub release: {result.LatestVersion}");
                }
            }
        }
        catch (Exception ex)
        {
            UpdateButton.Content = "Update check failed";
            _loggerService.Write($"WinUI update check failed: {ex}");
            if (!silent)
            {
                await ShowMessageAsync("Update check failed", ex.Message);
            }
        }
    }

    private async void CollectDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = _diagnosticsService.Collect(_appDataDirectory, CurrentVersion);
            StatusTextBlock.Text = $"Diagnostics created: {package}";
            await ShowMessageAsync("Diagnostics created", package);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Diagnostics failed", ex.Message);
        }
    }

    private async void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mem-Booster",
            "logs");
        Directory.CreateDirectory(logsPath);
        await Launcher.LaunchFolderPathAsync(logsPath);
    }

    private async void OpenGithub_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(RepositoryUrl));
    }

    private async void RunAsAdminButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _profileService.SaveLocalProfile(_selectedProcessNames);
            if (AdminService.RelaunchAsAdministrator())
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Admin relaunch failed", ex.Message);
        }
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        var theme = _isDarkTheme ? "Dark" : "Light";
        _profileService.SaveThemePreference(theme);
        ApplyTheme(theme, animate: true);
        StatusTextBlock.Text = $"{theme} theme enabled.";
    }

    private async Task<List<string>?> ShowDeviceOptionsDialogAsync(IReadOnlyList<DeviceOptimizationOption> options)
    {
        var checks = new List<CheckBox>();
        var panel = new StackPanel { Spacing = 10, MaxHeight = 560 };

        foreach (var option in options)
        {
            var suffix = option.RequiresRestart ? "  • restart required" : option.Advanced ? "  • advanced" : string.Empty;
            var check = new CheckBox
            {
                Content = $"{option.Name}{suffix}",
                IsChecked = option.DefaultSelected,
                Tag = option.Id
            };

            checks.Add(check);
            panel.Children.Add(check);

            var desc = option.WarningText is not null
                ? $"{option.Description} Warning: {option.WarningText}"
                : option.Description;
            panel.Children.Add(new TextBlock
            {
                Text = desc,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(28, -8, 0, 2)
            });
        }

        var scroller = new ScrollViewer { Content = panel, MaxHeight = 560 };

        var dialog = new ContentDialog
        {
            Title = "Device Optimise",
            Content = scroller,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            XamlRoot = RootGrid.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return checks
            .Where(c => c.IsChecked == true && c.Tag is string)
            .Select(c => (string)c.Tag)
            .ToList();
    }

    private async Task<bool> ShowTimedWarningAsync(string title, string message, int seconds)
    {
        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = text,
            PrimaryButtonText = $"Wait {seconds}s",
            CloseButtonText = "Cancel",
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootGrid.XamlRoot
        };

        var remaining = seconds;
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) =>
        {
            remaining--;
            if (remaining <= 0)
            {
                timer.Stop();
                dialog.PrimaryButtonText = "Continue";
                dialog.IsPrimaryButtonEnabled = true;
            }
            else
            {
                dialog.PrimaryButtonText = $"Wait {remaining}s";
            }
        };

        _loggerService.Write($"WinUI timed warning shown: title={title}; delaySeconds={seconds}");
        timer.Start();
        var result = await dialog.ShowAsync();
        timer.Stop();
        _loggerService.Write($"WinUI timed warning result: title={title}; accepted={result == ContentDialogResult.Primary}");
        return result == ContentDialogResult.Primary;
    }

    private async Task<bool> ShowConfirmAsync(string title, string message, string primary, string close)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary,
            CloseButtonText = close,
            XamlRoot = RootGrid.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void ShowProgress(string message, double value)
    {
        ProgressPanel.Visibility = Visibility.Visible;
        OperationProgress.Value = Math.Clamp(value, 0, 100);
        OperationProgressText.Text = message;
        if (_operationWatch.IsRunning)
        {
            ElapsedTextBlock.Text = $"{_operationWatch.Elapsed.TotalSeconds:0.0}s";
        }
    }

    private void HideProgress()
    {
        ProgressPanel.Visibility = Visibility.Collapsed;
        OperationProgress.Value = 0;
        OperationProgressText.Text = string.Empty;
    }

    private static string FormatBytes(long bytes)
    {
        var mb = bytes / 1024d / 1024d;
        return mb >= 1024 ? $"{mb / 1024d:0.0} GB" : $"{mb:0} MB";
    }
}
