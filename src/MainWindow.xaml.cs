using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
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
    private readonly LoggerService _loggerService;
    private readonly DispatcherTimer _memoryTimer;
    private readonly DispatcherTimer _processTimer;
    private readonly HashSet<string> _selectedProcessNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ICollectionView _processView;

    private bool _refreshRunning;
    private bool _syncingSelection;
    private string _memoryText = "Loading...";
    private double _memoryPercent;
    private string _runningText = "0";
    private string _selectedText = "0 active / 0 profile";
    private string _profileStatus = "No profile loaded";
    private string _statusText = "Ready";
    private string _searchText = string.Empty;

    public ObservableCollection<ProcessGroup> ProcessGroups { get; } = new();

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

    public MainWindow()
    {
        InitializeComponent();
        _loggerService = new LoggerService(_profileService.AppDataDirectory);

        _processView = CollectionViewSource.GetDefaultView(ProcessGroups);
        _processView.Filter = FilterProcess;
        _processView.SortDescriptions.Add(new SortDescription(nameof(ProcessGroup.WorkingSetBytes), ListSortDirection.Descending));
        _processView.SortDescriptions.Add(new SortDescription(nameof(ProcessGroup.DisplayName), ListSortDirection.Ascending));

        DataContext = this;

        LoadLocalProfileQuietly();
        UpdateMemoryInfo();

        _memoryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _memoryTimer.Tick += (_, _) => UpdateMemoryInfo();

        _processTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _processTimer.Tick += async (_, _) =>
        {
            if (AutoRefreshCheckBox.IsChecked == true && !ProcessesGrid.IsKeyboardFocusWithin)
            {
                await RefreshProcessesAsync();
            }
        };

        Loaded += async (_, _) =>
        {
            await RefreshProcessesAsync(force: true);
            _memoryTimer.Start();
            _processTimer.Start();
        };
    }

    private void LoadLocalProfileQuietly()
    {
        try
        {
            var profile = _profileService.LoadLocalProfile();
            _selectedProcessNames.Clear();
            foreach (var name in profile.ExecutableNames.Select(SafetyRules.NormaliseProcessName))
            {
                if (!SafetyRules.IsBlocked(name))
                {
                    _selectedProcessNames.Add(name);
                }
            }

            ProfileStatus = _selectedProcessNames.Count > 0
                ? $"Loaded local profile: {_selectedProcessNames.Count} entries"
                : "No local profile yet. Tick apps and save.";
        }
        catch (Exception ex)
        {
            ProfileStatus = $"Local profile could not be loaded: {ex.Message}";
        }
    }

    private async Task RefreshProcessesAsync(bool force = false)
    {
        if (_refreshRunning && !force)
        {
            return;
        }

        _refreshRunning = true;
        StatusText = "Refreshing running apps...";

        try
        {
            var snapshots = await Task.Run(() => _processService.GetProcessGroups());
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
            StatusText = $"Updated {DateTime.Now:T}";
        }
        catch (Exception ex)
        {
            StatusText = $"Refresh failed: {ex.Message}";
            _loggerService.Write($"Refresh failed: {ex}");
        }
        finally
        {
            _refreshRunning = false;
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
        }
        else
        {
            _selectedProcessNames.Remove(process.ExeName);
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var activeMatches = ProcessGroups.Count(p => p.IsSelected);
        SelectedText = $"{activeMatches} active / {_selectedProcessNames.Count} profile";
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
        UpdateMemoryInfo();
        await RefreshProcessesAsync(force: true);
    }

    private void SaveLocalButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _profileService.SaveLocalProfile(_selectedProcessNames);
            ProfileStatus = $"Saved local profile: {_selectedProcessNames.Count} entries";
            StatusText = "Local profile saved.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save Local Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportXmlButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProcessNames.Count == 0)
        {
            MessageBox.Show(this, "Tick at least one app before exporting a profile.", "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Information);
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
            _profileService.SaveProfile(dialog.FileName, new ProfileData("Gaming Boost", _selectedProcessNames));
            ProfileStatus = $"Exported XML profile: {_selectedProcessNames.Count} entries";
            StatusText = $"Exported: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void LoadXmlButton_Click(object sender, RoutedEventArgs e)
    {
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
            foreach (var name in profile.ExecutableNames.Select(SafetyRules.NormaliseProcessName))
            {
                if (!SafetyRules.IsBlocked(name))
                {
                    _selectedProcessNames.Add(name);
                }
            }

            ApplySelectionToRunningRows();
            await RefreshProcessesAsync(force: true);
            var activeMatches = ProcessGroups.Count(p => p.IsSelected);
            ProfileStatus = $"Loaded XML: {profile.Name}. {activeMatches} active matches, {_selectedProcessNames.Count - activeMatches} skipped/not running.";
            StatusText = "XML profile loaded.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Load XML Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectRecommendedButton_Click(object sender, RoutedEventArgs e)
    {
        var added = 0;
        foreach (var process in ProcessGroups.Where(p => p.CanSelect && p.IsRecommended))
        {
            if (_selectedProcessNames.Add(process.ExeName))
            {
                added++;
            }
        }

        ApplySelectionToRunningRows();
        ProfileStatus = $"Selected {added} recommended running background app(s). Review before boosting.";
        StatusText = "Recommended profile applied.";
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedProcessNames.Clear();
        ApplySelectionToRunningRows();
        ProfileStatus = "Selection cleared.";
        StatusText = "Profile cleared.";
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        ShowBoostPreview();
    }

    private void ShowBoostPreview()
    {
        if (_selectedProcessNames.Count == 0)
        {
            MessageBox.Show(this, "No apps selected. Tick apps or load an XML profile first.", "Boost Preview", MessageBoxButton.OK, MessageBoxImage.Information);
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
            $"Running matches to close: {runningMatches.Count}\nEstimated RAM currently used by matches: {estimatedMemory}\n\n{runningLines}\n\nProfile entries not running / skipped:\n{skippedText}",
            "Boost Preview",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void BoostButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProcessNames.Count == 0)
        {
            MessageBox.Show(this, "No apps selected. Tick apps or load an XML profile first.", "Boost", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var activeMatches = ProcessGroups.Count(p => p.IsSelected);
        var activeMemory = FormatBytes(ProcessGroups.Where(p => p.IsSelected).Sum(p => p.WorkingSetBytes));
        var confirm = MessageBox.Show(
            this,
            $"This will close matching running processes and their child/helper processes.\n\nProfile entries: {_selectedProcessNames.Count}\nCurrently running matches: {activeMatches}\nEstimated RAM in matched apps: {activeMemory}\n\nSmart close first: {(SmartCloseCheckBox.IsChecked == true ? "On" : "Off")}\n\nContinue?",
            "Boost Now",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _processTimer.Stop();
        StatusText = "Boost running... closing selected process trees.";

        try
        {
            var options = new TerminationOptions(SmartCloseCheckBox.IsChecked == true, 900, 1500);
            var selectedCsv = string.Join(", ", _selectedProcessNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            var result = await Task.Run(() => _processService.KillProcessTreesByExecutableNames(_selectedProcessNames, options));
            _loggerService.WriteBoost(selectedCsv, result);

            UpdateMemoryInfo();
            await RefreshProcessesAsync(force: true);

            var details = result.Messages.Count == 0
                ? string.Empty
                : "\n\nDetails:\n" + string.Join("\n", result.Messages.Take(8));

            MessageBox.Show(this, result.Summary + details, "Boost Complete", MessageBoxButton.OK,
                result.Failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            StatusText = result.Summary;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Boost Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = $"Boost failed: {ex.Message}";
            _loggerService.Write($"Boost failed: {ex}");
        }
        finally
        {
            if (AutoRefreshCheckBox.IsChecked == true)
            {
                _processTimer.Start();
            }
        }
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text.Trim();
        _processView.Refresh();
    }

    private void AutoRefreshCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_processTimer is null)
        {
            return;
        }

        if (AutoRefreshCheckBox.IsChecked == true)
        {
            _processTimer.Start();
            StatusText = "Auto-refresh enabled.";
        }
        else
        {
            _processTimer.Stop();
            StatusText = "Auto-refresh paused. Use Refresh manually.";
        }
    }

    private void OpenGithub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/ox1d3x3") { UseShellExecute = true });
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
