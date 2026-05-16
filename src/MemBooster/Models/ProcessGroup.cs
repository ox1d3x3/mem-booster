using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MemBooster.Models;

public sealed class ProcessGroup : INotifyPropertyChanged
{
    private string _displayName = string.Empty;
    private long _workingSetBytes;
    private int _instanceCount;
    private bool _isSelected;
    private bool _canSelect = true;
    private bool _isRecommended;
    private string _riskLabel = "Low";
    private string _riskDescription = string.Empty;

    public string ExeName { get; set; }

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    public long WorkingSetBytes
    {
        get => _workingSetBytes;
        set
        {
            if (SetField(ref _workingSetBytes, value))
            {
                OnPropertyChanged(nameof(MemoryText));
            }
        }
    }

    public int InstanceCount
    {
        get => _instanceCount;
        set => SetField(ref _instanceCount, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!CanSelect && value)
            {
                value = false;
            }

            SetField(ref _isSelected, value);
        }
    }

    public bool CanSelect
    {
        get => _canSelect;
        set
        {
            if (SetField(ref _canSelect, value) && !value && IsSelected)
            {
                IsSelected = false;
            }
        }
    }

    public bool IsRecommended
    {
        get => _isRecommended;
        set => SetField(ref _isRecommended, value);
    }

    public string RiskLabel
    {
        get => _riskLabel;
        set => SetField(ref _riskLabel, value);
    }

    public string RiskDescription
    {
        get => _riskDescription;
        set => SetField(ref _riskDescription, value);
    }

    public string MemoryText => FormatBytes(WorkingSetBytes);

    public ProcessGroup()
    {
        ExeName = string.Empty;
    }

    public ProcessGroup(string exeName)
    {
        ExeName = exeName;
    }

    public void UpdateFrom(ProcessGroupSnapshot snapshot, bool selected)
    {
        DisplayName = snapshot.DisplayName;
        WorkingSetBytes = snapshot.WorkingSetBytes;
        InstanceCount = snapshot.InstanceCount;
        CanSelect = snapshot.CanSelect;
        IsRecommended = snapshot.IsRecommended;
        RiskLabel = snapshot.RiskLabel;
        RiskDescription = snapshot.RiskDescription;
        IsSelected = selected && CanSelect;
    }

    private static string FormatBytes(long bytes)
    {
        var mb = bytes / 1024d / 1024d;
        return mb >= 1024
            ? $"{mb / 1024d:0.0} GB"
            : $"{mb:0} MB";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ProcessGroupSnapshot(
    string ExeName,
    string DisplayName,
    long WorkingSetBytes,
    int InstanceCount,
    bool CanSelect,
    bool IsRecommended,
    string RiskLabel,
    string RiskDescription);
