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
    private const string CurrentVersion = "0.6.19";
    private const string RepositoryUrl = "https://github.com/ox1d3x3/mem-booster";

    private readonly MemoryService _memoryService = new();
    private readonly ProcessService _processService = new();
    private readonly ProfileService _profileService;
    private readonly LoggerService _loggerService;
    private readonly UpdateService _updateService = new();
    private readonly DiagnosticsService _diagnosticsService;
    private readonly DeviceOptimizationService _deviceOptimizationService;
    private readonly ServiceControlService _serviceControlService;
    private readonly RespawnTracker _respawnTracker;

    // Boost session state: the long warning is shown once per session, then repeats are
    // instant. A new app service that would be stopped always re-shows the warning.
    private bool _boostWarningAcknowledged;
    private bool _continueWithoutAdmin;
    private readonly HashSet<string> _acknowledgedAppServices = new(StringComparer.OrdinalIgnoreCase);
    private int _boostVersion;

    private readonly HashSet<string> _selectedProcessNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProcessGroup> _allProcessGroups = new();
    private readonly Stopwatch _operationWatch = new();
    private readonly string _appDataDirectory;

    private bool _syncingSelection;
    private bool _busyOperation;
    private bool _isClosing;
    private bool _isDarkTheme = true;
    private bool _themeAnimationRunning;
    private string _searchText = string.Empty;
    private string? _pendingReleaseUrl;
    private DispatcherTimer? _searchDebounceTimer;

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
        _serviceControlService = new ServiceControlService(_appDataDirectory);
        _respawnTracker = new RespawnTracker(_appDataDirectory);

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
        RestoreServicesButton.IsEnabled = _serviceControlService.HasStoppedServices();

        RootGrid.Loaded += async (_, _) =>
        {
            _ = PlayEntranceAnimationAsync();

            await RefreshProcessesAsync("Initial load", true);
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                await CheckUpdatesAsync(silent: true);
            });
        };

        Closed += (_, _) => _isClosing = true;
    }

    // ═══════════════════════════════════════════
    // ENTRANCE + MICRO-INTERACTION ANIMATIONS
    // ═══════════════════════════════════════════

    private async Task PlayEntranceAnimationAsync()
    {
        await AnimateFadeSlideInAsync(HeaderCard, HeaderCardTranslate, 0);

        await Task.WhenAll(
            AnimateFadeSlideInAsync(MemoryCard, MemoryCardTranslate, 50),
            AnimateFadeSlideInAsync(ProcessCard, ProcessCardTranslate, 100),
            AnimateFadeSlideInAsync(SelectedCard, SelectedCardTranslate, 150),
            AnimateFadeSlideInAsync(ProfileCard, ProfileCardTranslate, 200));

        await Task.WhenAll(
            AnimateFadeSlideInAsync(ToolbarCard, ToolbarCardTranslate, 0),
            AnimateFadeSlideInAsync(ContentGrid, ContentGridTranslate, 60),
            AnimateFadeSlideInAsync(StatusBar, StatusBarTranslate, 120));

        _ = AnimateLogoPulseLoopAsync();
    }

    private static async Task AnimateFadeSlideInAsync(UIElement element, Microsoft.UI.Xaml.Media.TranslateTransform transform, int delayMs)
    {
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }

        var startY = transform.Y;
        const int frames = 18;
        for (var i = 1; i <= frames; i++)
        {
            var t = i / (double)frames;
            var eased = 1d - Math.Pow(1d - t, 3d); // cubic ease-out
            element.Opacity = eased;
            transform.Y = startY * (1d - eased);
            await Task.Delay(8);
        }

        element.Opacity = 1;
        transform.Y = 0;
    }

    private async Task AnimateLogoPulseLoopAsync()
    {
        while (!_isClosing)
        {
            for (var i = 1; i <= 18 && !_isClosing; i++)
            {
                LogoPulseRing.Opacity = (i / 18d) * 0.55d;
                await Task.Delay(20);
            }
            for (var i = 18; i >= 0 && !_isClosing; i--)
            {
                LogoPulseRing.Opacity = (i / 18d) * 0.55d;
                await Task.Delay(20);
            }
            await Task.Delay(2600);
        }
    }

    private async Task AnimateBoostButtonPressAsync()
    {
        const int frames = 9;
        for (var i = 1; i <= frames; i++)
        {
            var t = i / (double)frames;
            var scale = 1d - (0.06d * Math.Sin(t * Math.PI));
            BoostButtonScale.ScaleX = scale;
            BoostButtonScale.ScaleY = scale;
            await Task.Delay(6);
        }
        BoostButtonScale.ScaleX = 1;
        BoostButtonScale.ScaleY = 1;
    }

    private async Task AnimateBoostGlowAsync()
    {
        for (var pulse = 0; pulse < 3 && _busyOperation; pulse++)
        {
            for (var i = 1; i <= 10; i++)
            {
                BoostGlowRing.Opacity = i / 10d;
                await Task.Delay(14);
            }
            for (var i = 10; i >= 0; i--)
            {
                BoostGlowRing.Opacity = i / 10d;
                await Task.Delay(14);
            }
            await Task.Delay(90);
        }
        BoostGlowRing.Opacity = 0;
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

    // Single scan-and-merge path used by every refresh (manual, initial, post-boost),
    // so all of them get instance reuse and the in-place list diff.
    private async Task ReloadProcessGroupsAsync()
    {
        var snapshots = await Task.Run(() => _processService.GetProcessGroups());

        // Reuse existing ProcessGroup instances by exe name instead of recreating
        // everything: UpdateFrom mutates in place, so unchanged rows keep the same
        // object reference. Combined with the in-place diff in ApplyFilter, a
        // refresh only touches ListView containers for rows that actually changed,
        // instead of tearing down and rebuilding the whole list.
        var existingByName = _allProcessGroups.ToDictionary(g => g.ExeName, StringComparer.OrdinalIgnoreCase);

        _syncingSelection = true;
        List<ProcessGroup> ordered;
        try
        {
            ordered = snapshots
                .Select(s =>
                {
                    if (existingByName.TryGetValue(s.ExeName, out var group))
                    {
                        existingByName.Remove(s.ExeName);
                    }
                    else
                    {
                        group = new ProcessGroup(s.ExeName);
                        group.PropertyChanged += ProcessGroup_PropertyChanged;
                    }

                    group.UpdateFrom(s, _selectedProcessNames.Contains(s.ExeName));
                    group.ApplyRespawnTag(_respawnTracker.IsRespawner(s.ExeName));
                    return group;
                })
                .OrderByDescending(g => g.WorkingSetBytes)
                .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Whatever is left in existingByName is no longer running: unsubscribe.
            foreach (var gone in existingByName.Values)
            {
                gone.PropertyChanged -= ProcessGroup_PropertyChanged;
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

        // List is on screen now; fill in publishers in the background.
        _ = ResolvePublishersAsync();
    }

    private static string BuildChipSubtitle(ProcessGroup group)
    {
        return string.IsNullOrEmpty(group.Publisher)
            ? $"{group.ExeName} • {group.MemoryText}"
            : $"{group.ExeName} • {group.MemoryText} • {group.Publisher}";
    }

    private async Task ResolvePublishersAsync()
    {
        if (!_processService.HasPendingPublishers)
        {
            return;
        }

        IReadOnlyDictionary<string, string> resolved;
        try
        {
            resolved = await Task.Run(() => _processService.ResolvePendingPublishers());
        }
        catch (Exception ex)
        {
            _loggerService.Write($"Publisher lookup failed: {ex.Message}");
            return;
        }

        if (resolved.Count == 0 || _isClosing)
        {
            return;
        }

        foreach (var group in _allProcessGroups)
        {
            if (!resolved.TryGetValue(group.ExeName, out var publisher) || group.Publisher == publisher)
            {
                continue;
            }

            group.Publisher = publisher; // Row updates via INotifyPropertyChanged.

            // Swap just this chip so the sidebar doesn't re-animate every item.
            for (var i = 0; i < SelectedApps.Count; i++)
            {
                if (string.Equals(SelectedApps[i].ExeName, group.ExeName, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedApps[i] = new SelectedAppItem(group.DisplayName, group.ExeName, BuildChipSubtitle(group));
                    break;
                }
            }
        }

        // Publisher is searchable, so re-apply an active search with the new data.
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            ApplyFilter();
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

            await ReloadProcessGroupsAsync();

            watch.Stop();
            StatusTextBlock.Text = $"Running apps refreshed in {watch.ElapsedMilliseconds} ms.";
            if (!string.Equals(context, "Manual refresh", StringComparison.OrdinalIgnoreCase) || watch.ElapsedMilliseconds >= 50)
            {
                _loggerService.Write($"WinUI refresh complete: context={context}; groups={_allProcessGroups.Count}; visible={ProcessGroups.Count}; selected={_selectedProcessNames.Count}; elapsedMs={watch.ElapsedMilliseconds}");
            }

            if (showProgress)
            {
                ShowProgress($"Loaded {ProcessGroups.Count} app group(s)", 100);
                await Task.Delay(120);
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

        // Compute the target visible set in order.
        var desired = new List<ProcessGroup>(_allProcessGroups.Count);
        foreach (var group in _allProcessGroups)
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                var match = group.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || group.ExeName.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || group.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || group.RiskLabel.Contains(query, StringComparison.OrdinalIgnoreCase);

                if (!match)
                {
                    continue;
                }
            }

            desired.Add(group);
        }

        // Apply a minimal diff against the current ObservableCollection instead of
        // Clear()+Add() — that rebuilds every ListView container and is the slow path.
        // Removing stale rows and inserting new matches in place keeps containers alive
        // and lets the built-in list transitions animate only what actually changed.
        var desiredSet = new HashSet<ProcessGroup>(desired);

        for (var i = ProcessGroups.Count - 1; i >= 0; i--)
        {
            if (!desiredSet.Contains(ProcessGroups[i]))
            {
                ProcessGroups.RemoveAt(i);
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var target = desired[i];
            if (i >= ProcessGroups.Count)
            {
                ProcessGroups.Add(target);
            }
            else if (!ReferenceEquals(ProcessGroups[i], target))
            {
                ProcessGroups.Insert(i, target);
            }
        }

        _ = AnimateProcessCountAsync(_allProcessGroups.Count);
    }

    private int _displayedProcessCount;
    private int _processCountAnimVersion;

    // Count-up/down animation for the Running apps stat. A version stamp cancels any
    // in-flight animation when a newer one starts, so rapid refreshes never fight.
    private async Task AnimateProcessCountAsync(int target)
    {
        var version = ++_processCountAnimVersion;
        var start = _displayedProcessCount;
        if (start == target)
        {
            ProcessCountTextBlock.Text = target.ToString();
            return;
        }

        const int frames = 14;
        for (var i = 1; i <= frames; i++)
        {
            if (version != _processCountAnimVersion)
            {
                return;
            }

            var t = i / (double)frames;
            var eased = 1d - Math.Pow(1d - t, 3d);
            _displayedProcessCount = (int)Math.Round(start + ((target - start) * eased));
            ProcessCountTextBlock.Text = _displayedProcessCount.ToString();
            await Task.Delay(14);
        }

        _displayedProcessCount = target;
        ProcessCountTextBlock.Text = target.ToString();
    }

    private void RefreshMemory()
    {
        var info = _memoryService.GetMemoryInfo();
        MemoryTextBlock.Text = info.Summary;
        ApplyMemoryBarColor(info.UsedPercent);
        _ = AnimateMemoryBarAsync(info.UsedPercent);
    }

    // Green while comfortable, amber under pressure, red when critical — an instant
    // visual read on whether a boost is worth running.
    private void ApplyMemoryBarColor(double usedPercent)
    {
        var color = usedPercent >= 85d
            ? Windows.UI.Color.FromArgb(255, 0xEF, 0x44, 0x44)
            : usedPercent >= 60d
                ? Windows.UI.Color.FromArgb(255, 0xF5, 0x9E, 0x0B)
                : Windows.UI.Color.FromArgb(255, 0x22, 0xC5, 0x5E);

        if (MemoryBar.Foreground is SolidColorBrush brush && brush.Color == color)
        {
            return;
        }

        MemoryBar.Foreground = new SolidColorBrush(color);
    }

    private async Task AnimateMemoryBarAsync(double target)
    {
        var start = MemoryBar.Value;
        var delta = target - start;
        if (Math.Abs(delta) < 0.5d)
        {
            MemoryBar.Value = target;
            return;
        }

        const int frames = 16;
        for (var i = 1; i <= frames; i++)
        {
            var t = i / (double)frames;
            var eased = 1d - Math.Pow(1d - t, 3d);
            MemoryBar.Value = start + (delta * eased);
            await Task.Delay(10);
        }
        MemoryBar.Value = target;
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
            AddSelectedAppItem(group);
        }
        else
        {
            _selectedProcessNames.Remove(group.ExeName);
            RemoveSelectedAppItem(group.ExeName);
        }

        UpdateSelectionCounts();
    }

    // Insert a single chip in the right place (sorted by working set, then name)
    // instead of clearing and rebuilding the whole sidebar collection.
    private void AddSelectedAppItem(ProcessGroup group)
    {
        var existing = SelectedApps.FirstOrDefault(a => string.Equals(a.ExeName, group.ExeName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return;
        }

        var item = new SelectedAppItem(
            group.DisplayName,
            group.ExeName,
            BuildChipSubtitle(group));

        var insertIndex = 0;
        while (insertIndex < SelectedApps.Count)
        {
            var current = _allProcessGroups.FirstOrDefault(g => string.Equals(g.ExeName, SelectedApps[insertIndex].ExeName, StringComparison.OrdinalIgnoreCase));
            var currentWeight = current?.WorkingSetBytes ?? 0;
            if (group.WorkingSetBytes > currentWeight)
            {
                break;
            }

            insertIndex++;
        }

        SelectedApps.Insert(insertIndex, item);
    }

    private void RemoveSelectedAppItem(string exeName)
    {
        var existing = SelectedApps.FirstOrDefault(a => string.Equals(a.ExeName, exeName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedApps.Remove(existing);
        }
    }

    private void UpdateSelectionCounts()
    {
        var running = 0;
        long reclaimableBytes = 0;
        foreach (var group in _allProcessGroups)
        {
            if (_selectedProcessNames.Contains(group.ExeName))
            {
                running++;

                // Apps that come straight back don't free memory for long; leave them out.
                if (!group.Respawns)
                {
                    reclaimableBytes += group.WorkingSetBytes;
                }
            }
        }

        var total = _selectedProcessNames.Count;
        var text = total == 0 ? "0 selected" : $"{running} active / {total} apps";
        SelectedTextBlock.Text = text;
        SelectedPanelTextBlock.Text = text;

        // Estimated RAM the boost would free right now — the number gamers care about.
        if (reclaimableBytes > 0)
        {
            SelectedReclaimTextBlock.Text = $"\u2248 {FormatBytesShort(reclaimableBytes)} reclaimable";
            SelectedReclaimTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            SelectedReclaimTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private static string FormatBytesShort(long bytes)
    {
        var mb = bytes / 1024d / 1024d;
        return mb >= 1024d ? $"{mb / 1024d:0.0} GB" : $"{mb:0} MB";
    }

    // Full rebuild — used after bulk operations (refresh, Safe/Extreme/Aggressive
    // Select, Load XML) where many items change at once. Single toggles use the
    // incremental Add/Remove path above instead.
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
                BuildChipSubtitle(group)));
        }

        UpdateSelectionCounts();

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

        // Round-trip rule: anything the user can manually tick in the UI must survive
        // save -> load. Only hard-blocked (protected) names are refused, matching the
        // manual selection rules exactly. Safe/Extreme/Aggressive stay unaffected —
        // their input lists are already pre-filtered before reaching here.
        var skippedProtected = 0;
        foreach (var exe in executableNames)
        {
            var name = SafetyRules.NormaliseProcessName(exe);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (SafetyRules.IsBlocked(name))
            {
                skippedProtected++;
                continue;
            }

            _selectedProcessNames.Add(name);
        }

        UpdateSelectionSummary();
        ApplyFilter();

        var running = _allProcessGroups.Count(g => _selectedProcessNames.Contains(g.ExeName));
        var summary = $"{mode}: {_selectedProcessNames.Count} selected, {running} currently running.";
        if (skippedProtected > 0)
        {
            summary += $" Skipped {skippedProtected} protected entr{(skippedProtected == 1 ? "y" : "ies")}.";
        }

        ProfileStatusTextBlock.Text = summary;

        // Logging a bulk selection can write hundreds of lines. Snapshot the data and
        // write it off the UI thread so Safe/Extreme/Aggressive Select stay instant.
        var selectedSnapshot = _selectedProcessNames.ToArray();
        var groupSnapshot = _allProcessGroups.ToArray();
        _ = Task.Run(() => _loggerService.WriteSelection($"WinUI {mode}", selectedSnapshot, groupSnapshot));
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

        // Debounce: only re-filter once the user pauses typing. This avoids a full
        // list teardown/rebuild on every keystroke, which is the costly part.
        _searchDebounceTimer ??= CreateSearchDebounceTimer();
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private DispatcherTimer CreateSearchDebounceTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ApplyFilter();
        };
        return timer;
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
            RemoveSelectedAppItem(item.ExeName);
            UpdateSelectionCounts();

            // Untick the matching row without rebuilding the whole list.
            var group = _allProcessGroups.FirstOrDefault(g => string.Equals(g.ExeName, item.ExeName, StringComparison.OrdinalIgnoreCase));
            if (group is not null && group.IsSelected)
            {
                _syncingSelection = true;
                try { group.IsSelected = false; }
                finally { _syncingSelection = false; }
            }

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
        StatusTextBlock.Text = "Selection cleared.";
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

        _ = AnimateBoostButtonPressAsync();

        var runningTargets = _allProcessGroups
            .Where(p => _selectedProcessNames.Contains(p.ExeName))
            .ToList();

        if (runningTargets.Count == 0)
        {
            await ShowMessageAsync("Nothing to boost", "No selected apps are currently running.");
            return;
        }

        var elevated = AdminService.IsCurrentProcessElevated();
        if (!elevated && !_continueWithoutAdmin)
        {
            var adminChoice = await ShowConfirmAsync(
                "Run as administrator?",
                "Mem-Booster is currently in Standard mode. Some elevated apps may refuse to close, and app services can't be stopped. Relaunch as administrator now?",
                "Relaunch",
                "Continue");
            if (!adminChoice)
            {
                _continueWithoutAdmin = true; // Don't ask again this session.
            }
            else
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

        // Selected apps that run as Windows services get stopped through the Service
        // Control Manager instead of killed: a killed service is restarted by Windows
        // within seconds, a stopped one stays stopped. Needs admin, so only when elevated.
        var selectionForServices = _selectedProcessNames.ToArray();
        IReadOnlyList<AppServiceTarget> appServices = Array.Empty<AppServiceTarget>();
        if (elevated)
        {
            try
            {
                appServices = await Task.Run(() => _serviceControlService.FindServicesForApps(selectionForServices));
            }
            catch (Exception ex)
            {
                _loggerService.WriteServices($"App service lookup failed: {ex.Message}");
            }
        }

        var unseenServices = appServices.Where(t => !_acknowledgedAppServices.Contains(t.ServiceName)).ToList();
        if (!_boostWarningAcknowledged || unseenServices.Count > 0)
        {
            var selectedMemory = runningTargets.Sum(p => p.WorkingSetBytes);
            var warning = $"Mem-Booster will close {runningTargets.Count} selected running app group(s).{Environment.NewLine}{Environment.NewLine}" +
                          $"Estimated selected working set: {FormatBytes(selectedMemory)}{Environment.NewLine}{Environment.NewLine}";

            if (appServices.Count > 0)
            {
                var names = string.Join(", ", appServices.Select(t => t.DisplayName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
                warning += $"{appServices.Count} of these run as Windows services ({names}). They'll be stopped properly so Windows " +
                           $"doesn't restart them. Use Restore Services to bring them back.{Environment.NewLine}{Environment.NewLine}";
            }

            warning += "Save your work first. Heavy boost may stop background sync, helpers, web views, editors or work apps. " +
                       "Restart Windows after gaming if you want the normal background-app state back.";

            var accepted = await ShowTimedWarningAsync("Boost Now Warning", warning, 5);
            if (!accepted)
            {
                StatusTextBlock.Text = "Boost cancelled.";
                return;
            }

            _boostWarningAcknowledged = true;
            foreach (var target in appServices)
            {
                _acknowledgedAppServices.Add(target.ServiceName);
            }
        }

        _busyOperation = true;
        _operationWatch.Restart();
        _ = AnimateBoostGlowAsync();

        try
        {
            ShowProgress("Preparing boost...", 10);

            // Freeze the selection: the background work must never enumerate the live
            // HashSet while the UI thread could still change it.
            var targets = _selectedProcessNames.ToArray();
            var groupsBefore = _allProcessGroups.ToArray();

            // The before-boost CSV is diagnostics only — write it alongside the boost
            // instead of making the user wait for disk I/O before anything closes.
            var beforeSnapshotTask = Task.Run(() =>
            {
                try
                {
                    var before = _loggerService.WriteProcessSnapshot("before-boost", groupsBefore, targets);
                    _loggerService.Write($"WinUI before-boost snapshot: {before}");
                }
                catch (Exception ex)
                {
                    _loggerService.Write($"WinUI before-boost snapshot failed: {ex.Message}");
                }
            });

            var fastBoost = FastBoostCheckBox.IsChecked == true;
            var smartClose = SmartCloseCheckBox.IsChecked == true && !fastBoost;
            var options = fastBoost
                ? new TerminationOptions(false, 0, 900)
                : new TerminationOptions(smartClose, 750, 1200);

            var serviceTrace = new LogBuffer();
            ServiceStopResult? serviceResult = null;
            var serviceClosedExes = new List<string>();
            if (appServices.Count > 0)
            {
                ShowProgress($"Stopping {appServices.Count} app service(s)...", 30);
                serviceResult = await Task.Run(() => _serviceControlService.StopAppServices(appServices, line => serviceTrace.Add(line)));
                var stoppedNames = new HashSet<string>(serviceResult.StoppedServiceNames, StringComparer.OrdinalIgnoreCase);
                serviceClosedExes.AddRange(appServices.Where(t => stoppedNames.Contains(t.ServiceName)).Select(t => t.ExeName));
                RestoreServicesButton.IsEnabled = _serviceControlService.HasStoppedServices();
            }

            ShowProgress("Closing selected app process trees...", 45);

            // Buffer trace lines in memory and flush once, rather than one locked file
            // append per process while the parallel close is running.
            var traceLines = new LogBuffer();
            var result = await Task.Run(() => _processService.KillProcessTreesByExecutableNames(
                targets,
                options,
                line => traceLines.Add($"WinUI boost: {line}")));

            var selectedLabel = string.Join(", ", targets.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            var serviceResultForLog = serviceResult;
            _ = Task.Run(async () =>
            {
                await beforeSnapshotTask;
                _loggerService.WritePerformanceBatch(traceLines);
                _loggerService.WriteBoost(selectedLabel, result);
                if (serviceResultForLog is not null)
                {
                    _loggerService.WriteServicesBatch(serviceTrace);
                    _loggerService.WriteServicesBatch(serviceResultForLog.Messages
                        .Select(m => "  " + m)
                        .Prepend("Boost app services: " + serviceResultForLog.Summary));
                }
            });

            ShowProgress("Refreshing memory and running app list...", 80);
            await RefreshProcessesAfterBoostAsync();

            ShowProgress("Boost complete.", 100);
            var serviceNote = serviceResult is { Stopped: > 0 }
                ? $" Stopped {serviceResult.Stopped} app service(s); use Restore Services to bring them back."
                : string.Empty;
            StatusTextBlock.Text = $"Boost complete: {result.Summary}.{serviceNote} Re-checking in a few seconds...";
            ElapsedTextBlock.Text = $"{_operationWatch.Elapsed.TotalSeconds:0.0}s";

            // Re-scan shortly after so apps Windows restarts show up, and tag them.
            var closedThisBoost = result.ClosedExeNames.Concat(serviceClosedExes).ToArray();
            _ = CheckForRespawnsAsync(closedThisBoost, ++_boostVersion);
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
            await Task.Delay(150);
            HideProgress();
            _busyOperation = false;
        }
    }

    private async Task CheckForRespawnsAsync(IReadOnlyCollection<string> closedExeNames, int version)
    {
        if (closedExeNames.Count == 0)
        {
            return;
        }

        try
        {
            // Long enough for Windows service recovery and shell restarts to kick in.
            await Task.Delay(4000);

            // Wait briefly if the user started something else; give up rather than interfere.
            for (var attempt = 0; attempt < 3 && _busyOperation; attempt++)
            {
                await Task.Delay(1500);
            }

            if (_isClosing || _busyOperation || version != _boostVersion)
            {
                return;
            }

            _busyOperation = true;
            try
            {
                RefreshMemory();
                await ReloadProcessGroupsAsync();

                var runningNow = new HashSet<string>(_allProcessGroups.Select(g => g.ExeName), StringComparer.OrdinalIgnoreCase);
                var cameBack = closedExeNames.Where(runningNow.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var newlyTagged = _respawnTracker.Update(closedExeNames, runningNow);

                foreach (var group in _allProcessGroups)
                {
                    if (_respawnTracker.IsRespawner(group.ExeName))
                    {
                        group.ApplyRespawnTag(true);
                    }
                }

                UpdateSelectionCounts();

                if (cameBack.Count == 0)
                {
                    StatusTextBlock.Text = "Re-check: everything the boost closed has stayed closed.";
                }
                else
                {
                    var list = string.Join(", ", cameBack.Take(6)) + (cameBack.Count > 6 ? $" +{cameBack.Count - 6} more" : string.Empty);
                    StatusTextBlock.Text = $"Re-check: {cameBack.Count} app(s) came straight back ({list}). Marked as Respawns.";
                }

                var newlyText = newlyTagged.Count > 0 ? string.Join(", ", newlyTagged) : "none";
                _ = Task.Run(() => _loggerService.Write(
                    $"Respawn check: closed={closedExeNames.Count}; cameBack={cameBack.Count} [{string.Join(", ", cameBack)}]; newlyTagged={newlyText}"));
            }
            finally
            {
                _busyOperation = false;
            }
        }
        catch (Exception ex)
        {
            _loggerService.Write($"Respawn check failed: {ex.Message}");
        }
    }

    private async Task RefreshProcessesAfterBoostAsync()
    {
        RefreshMemory();
        await ReloadProcessGroupsAsync();

        var groupsAfter = _allProcessGroups.ToArray();
        var selectedAfter = _selectedProcessNames.ToArray();
        _ = Task.Run(() =>
        {
            try
            {
                _loggerService.WriteProcessSnapshot("after-boost", groupsAfter, selectedAfter);
            }
            catch (Exception ex)
            {
                _loggerService.Write($"WinUI after-boost snapshot failed: {ex.Message}");
            }
        });
    }

    private void SaveLocalButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProcessNames.Count == 0)
        {
            StatusTextBlock.Text = "Nothing to save — select at least one app first.";
            return;
        }

        _profileService.SaveLocalProfile(_selectedProcessNames);
        ProfileStatusTextBlock.Text = $"Current profile saved with {_selectedProcessNames.Count} app(s).";
        StatusTextBlock.Text = "Current profile saved to this PC.";
    }

    private async void ExportXmlButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProcessNames.Count == 0)
        {
            await ShowMessageAsync("Nothing to export", "Select at least one app before exporting a profile.");
            return;
        }

        try
        {
            var filePath = await PickSaveProfilePathAsync();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _profileService.SaveProfile(filePath, new ProfileData("Gaming Boost", _selectedProcessNames));
            StatusTextBlock.Text = $"Profile exported ({_selectedProcessNames.Count} app(s)): {Path.GetFileName(filePath)}";
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

    // ═══════════════════════════════════════════
    // SERVICES
    // ═══════════════════════════════════════════

    private async void StopServicesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busyOperation)
        {
            return;
        }

        if (!await EnsureElevatedAsync("Stop Services", "Stopping Windows services requires administrator rights."))
        {
            return;
        }

        var serviceTrace = new LogBuffer();
        var running = await Task.Run(() => _serviceControlService.GetStoppableRunningServices(line => serviceTrace.Add(line)));
        if (running.Count == 0)
        {
            _ = Task.Run(() => _loggerService.WriteServicesBatch(serviceTrace));
            await ShowMessageAsync("Nothing to stop", "None of the services Mem-Booster can safely stop are running right now.");
            return;
        }

        var chosen = await ShowServicesDialogAsync(running);
        if (chosen is null || chosen.Count == 0)
        {
            StatusTextBlock.Text = "Stop Services cancelled.";
            return;
        }

        _busyOperation = true;
        try
        {
            ShowProgress($"Stopping {chosen.Count} service(s)...", 30);
            var result = await Task.Run(() => _serviceControlService.StopServices(chosen, line => serviceTrace.Add(line)));

            RestoreServicesButton.IsEnabled = _serviceControlService.HasStoppedServices();
            StatusTextBlock.Text = $"{result.Summary}. Use Restore Services to bring them back, or restart Windows.";
            _ = Task.Run(() =>
            {
                _loggerService.WriteServicesBatch(serviceTrace);
                _loggerService.WriteServicesBatch(result.Messages.Select(m => "  " + m).Prepend(result.Summary));
            });

            ShowProgress("Services stopped.", 100);
            RefreshMemory();

            if (result.Messages.Count > 0)
            {
                await ShowMessageAsync("Stop Services", $"{result.Summary}\n\n{string.Join("\n", result.Messages.Take(12))}");
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Stop Services failed: {ex.Message}";
            _loggerService.WriteServices("Stop failed: " + ex);
            await ShowMessageAsync("Stop Services failed", ex.Message);
        }
        finally
        {
            await Task.Delay(150);
            HideProgress();
            _busyOperation = false;
        }
    }

    private async void RestoreServicesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busyOperation)
        {
            return;
        }

        if (!_serviceControlService.HasStoppedServices())
        {
            RestoreServicesButton.IsEnabled = false;
            await ShowMessageAsync("Nothing to restore", "Mem-Booster has no stopped services on record.");
            return;
        }

        if (!await EnsureElevatedAsync("Restore Services", "Starting Windows services requires administrator rights."))
        {
            return;
        }

        _busyOperation = true;
        try
        {
            ShowProgress("Restoring services...", 30);
            var serviceTrace = new LogBuffer();
            var result = await Task.Run(() => _serviceControlService.RestoreServices(line => serviceTrace.Add(line)));

            RestoreServicesButton.IsEnabled = _serviceControlService.HasStoppedServices();
            StatusTextBlock.Text = result.Summary + ".";
            _ = Task.Run(() =>
            {
                _loggerService.WriteServicesBatch(serviceTrace);
                _loggerService.WriteServicesBatch(result.Messages.Select(m => "  " + m).Prepend(result.Summary));
            });

            ShowProgress("Services restored.", 100);

            if (result.Failed > 0 || result.Messages.Count > 0)
            {
                await ShowMessageAsync("Restore Services", $"{result.Summary}\n\n{string.Join("\n", result.Messages.Take(12))}");
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Restore Services failed: {ex.Message}";
            _loggerService.WriteServices("Restore failed: " + ex);
            await ShowMessageAsync("Restore Services failed", ex.Message);
        }
        finally
        {
            await Task.Delay(150);
            HideProgress();
            _busyOperation = false;
        }
    }

    private async Task<bool> EnsureElevatedAsync(string feature, string reason)
    {
        if (AdminService.IsCurrentProcessElevated())
        {
            return true;
        }

        var relaunch = await ShowConfirmAsync(
            "Administrator mode required",
            $"{reason} Relaunch Mem-Booster as administrator to use {feature}?",
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

        return false;
    }

    private async Task<List<string>?> ShowServicesDialogAsync(IReadOnlyList<RunningServiceInfo> services)
    {
        var checks = new List<CheckBox>();
        var panel = new StackPanel { Spacing = 10 };

        panel.Children.Add(new TextBlock
        {
            Text = "Only services on Mem-Booster's safe list are shown. Startup settings are never changed — " +
                   "everything returns to normal with Restore Services or a restart.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.8
        });

        ServiceTier? currentTier = null;
        foreach (var service in services)
        {
            if (currentTier != service.Candidate.Tier)
            {
                currentTier = service.Candidate.Tier;
                panel.Children.Add(new TextBlock
                {
                    Text = currentTier == ServiceTier.Recommended ? "Recommended" : "Optional — review before stopping",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }

            var check = new CheckBox
            {
                Content = service.DisplayName,
                IsChecked = service.Candidate.DefaultSelected,
                Tag = service.Candidate.ServiceName
            };

            checks.Add(check);
            panel.Children.Add(check);
            panel.Children.Add(new TextBlock
            {
                Text = $"{service.Candidate.ServiceName} • {service.Candidate.Description}",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(28, -8, 0, 2)
            });
        }

        var dialog = new ContentDialog
        {
            Title = $"Stop Services ({services.Count} running)",
            Content = new ScrollViewer { Content = panel, MaxHeight = 560 },
            PrimaryButtonText = "Stop selected",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return checks
            .Where(c => c.IsChecked == true && c.Tag is string)
            .Select(c => (string)c.Tag)
            .ToList();
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
