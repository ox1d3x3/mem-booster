using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MemBooster.Models;

public sealed class ProcessGroup : INotifyPropertyChanged
{
    private string _displayName = string.Empty;
    private string _publisher = string.Empty;
    private long _memoryBytes;
    private int _instanceCount;
    private bool _isSelected;
    private bool _canSelect = true;
    private string _riskLabel = "Low";
    private string _riskDescription = string.Empty;

    public string ExeName { get; set; }

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    public string Publisher
    {
        get => _publisher;
        set => SetField(ref _publisher, value);
    }

    /// <summary>Private working set summed over every instance (what Task Manager shows).</summary>
    public long MemoryBytes
    {
        get => _memoryBytes;
        set
        {
            if (SetField(ref _memoryBytes, value))
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

    public string MemoryText => FormatBytes(MemoryBytes);

    public ProcessGroup()
    {
        ExeName = string.Empty;
    }

    public ProcessGroup(string exeName)
    {
        ExeName = exeName;
    }

    /// <summary>True when this app came straight back after a previous boost closed it.</summary>
    public bool Respawns { get; private set; }

    /// <summary>
    /// Overrides the status after <see cref="UpdateFrom"/> (which resets it) when the app
    /// is known to come back after being closed.
    /// </summary>
    public void ApplyRespawnTag(bool respawns)
    {
        Respawns = respawns;
        if (respawns)
        {
            RiskLabel = "Respawns";
            RiskDescription = "Comes straight back when closed, so boosts trim its memory instead of closing it";
        }
    }

    public void UpdateFrom(ProcessGroupSnapshot snapshot, bool selected)
    {
        DisplayName = snapshot.DisplayName;
        Publisher = snapshot.Publisher;
        MemoryBytes = snapshot.MemoryBytes;
        InstanceCount = snapshot.InstanceCount;
        CanSelect = snapshot.CanSelect;
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
    string Publisher,
    long MemoryBytes,
    int InstanceCount,
    bool CanSelect,
    string RiskLabel,
    string RiskDescription);
