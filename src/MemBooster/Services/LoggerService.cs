using System.Diagnostics;
using System.IO;
using System.Text;
using MemBooster.Models;

namespace MemBooster.Services;

public sealed class LoggerService
{
    private const long MaxLogBytes = 5L * 1024L * 1024L;
    private const int MaxSnapshotFiles = 40; // ~20 boosts (one before + one after each).
    private readonly string _logDirectory;
    private readonly object _sync = new();

    public LoggerService(string appDataDirectory)
    {
        _logDirectory = Path.Combine(appDataDirectory, "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    public string ActivityLogPath => Path.Combine(_logDirectory, "mem-booster.log");
    public string BoostLogPath => Path.Combine(_logDirectory, "boost.log");
    public string PerformanceLogPath => Path.Combine(_logDirectory, "performance.log");
    public string SnapshotDirectory => Path.Combine(_logDirectory, "snapshots");
    public string DeviceOptimiseLogPath => Path.Combine(_logDirectory, "device-optimise.log");

    public void Write(string message) => WriteLine(ActivityLogPath, message);

    public void WritePerformance(string message) => WriteLine(PerformanceLogPath, message);

    public void WriteDeviceOptimise(string message) => WriteLine(DeviceOptimiseLogPath, message);

    public string ServicesLogPath => Path.Combine(_logDirectory, "services.log");

    public void WriteServices(string message) => WriteLine(ServicesLogPath, message);

    // One locked append for a whole batch instead of one file open per line.
    public void WriteServicesBatch(IEnumerable<string> messages) => WriteLines(ServicesLogPath, messages);

    // Buffered lines keep the time they were recorded, not the time they were flushed.
    public void WritePerformanceBatch(LogBuffer buffer) => WriteStampedLines(PerformanceLogPath, buffer.Drain());

    public void WriteServicesBatch(LogBuffer buffer) => WriteStampedLines(ServicesLogPath, buffer.Drain());

    public void WriteBoost(string selected, BoostResult result)
    {
        var summary = $"Boost profile=[{selected}] => {result.Summary}";
        Write(summary);
        WriteLines(BoostLogPath, result.Messages.Take(400).Select(line => "  " + line).Prepend(summary));
    }

    public void WriteSelection(string mode, IReadOnlyCollection<string> selectedNames, IEnumerable<ProcessGroup> processGroups)
    {
        var selected = selectedNames
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectedSet = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
        var running = processGroups
            .Where(p => selectedSet.Contains(p.ExeName))
            .OrderByDescending(p => p.MemoryBytes)
            .ToList();

        var estimatedRam = running.Sum(p => p.MemoryBytes);
        var header = $"Selection mode={mode}; selected={selected.Count}; runningMatches={running.Count}; estimatedMemory={FormatBytes(estimatedRam)}";
        WriteLines(ActivityLogPath, running.Take(250)
            .Select(process => $"  selected-running: {process.DisplayName} | {process.ExeName} | {process.MemoryText} | instances={process.InstanceCount} | status={process.RiskLabel}")
            .Prepend(header));
    }

    public string WriteProcessSnapshot(string label, IEnumerable<ProcessGroup> processGroups, IReadOnlyCollection<string>? selectedNames = null)
    {
        Directory.CreateDirectory(SnapshotDirectory);
        var safeLabel = string.Concat(label.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-'));
        var path = Path.Combine(SnapshotDirectory, $"{DateTime.Now:yyyyMMdd-HHmmssfff}-{safeLabel}.csv");
        var selected = new HashSet<string>(selectedNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var timestamp = Csv(DateTime.Now.ToString("O"));

        var builder = new StringBuilder();
        builder.AppendLine("Timestamp,Label,Selected,CanSelect,DisplayName,ExeName,Publisher,MemoryBytes,MemoryText,Instances,Status,Description");
        foreach (var process in processGroups.OrderByDescending(p => p.MemoryBytes).ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(string.Join(",", new[]
            {
                timestamp,
                Csv(label),
                Csv((process.IsSelected || selected.Contains(process.ExeName)).ToString()),
                Csv(process.CanSelect.ToString()),
                Csv(process.DisplayName),
                Csv(process.ExeName),
                Csv(process.Publisher),
                Csv(process.MemoryBytes.ToString()),
                Csv(process.MemoryText),
                Csv(process.InstanceCount.ToString()),
                Csv(process.RiskLabel),
                Csv(process.RiskDescription)
            }));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        Write($"Process snapshot saved: {path}");
        PruneSnapshots();
        return path;
    }

    // Keep only the newest snapshots so the folder doesn't grow forever. File names start
    // with a sortable timestamp, so name order is age order.
    private void PruneSnapshots()
    {
        try
        {
            var files = new DirectoryInfo(SnapshotDirectory).GetFiles("*.csv");
            if (files.Length <= MaxSnapshotFiles)
            {
                return;
            }

            foreach (var file in files.OrderByDescending(f => f.Name, StringComparer.Ordinal).Skip(MaxSnapshotFiles))
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // In use or already gone; try again next time.
                }
            }
        }
        catch
        {
            // Housekeeping must never break the app.
        }
    }

    private void WriteLine(string path, string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            lock (_sync)
            {
                RotateIfOversized(path);
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never break the app.
        }
    }

    private void WriteLines(string path, IEnumerable<string> messages)
    {
        try
        {
            var stamp = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ";
            var builder = new StringBuilder();
            foreach (var message in messages)
            {
                builder.Append(stamp).Append(message).Append(Environment.NewLine);
            }

            if (builder.Length == 0)
            {
                return;
            }

            lock (_sync)
            {
                RotateIfOversized(path);
                File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never break the app.
        }
    }

    private void WriteStampedLines(string path, IReadOnlyList<string> stampedLines)
    {
        if (stampedLines.Count == 0)
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            foreach (var line in stampedLines)
            {
                builder.Append(line).Append(Environment.NewLine);
            }

            lock (_sync)
            {
                RotateIfOversized(path);
                File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never break the app.
        }
    }

    private static void RotateIfOversized(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var info = new FileInfo(path);
            if (info.Length < MaxLogBytes)
            {
                return;
            }

            var archivePath = path + ".old";
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            File.Move(path, archivePath);
            File.WriteAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Log rotated. Previous log moved to {Path.GetFileName(archivePath)}.{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // Log rotation must never break the app.
        }
    }

    private static string Csv(string value)
    {
        value ??= string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string FormatBytes(long bytes)
    {
        var mb = bytes / 1024d / 1024d;
        return mb >= 1024 ? $"{mb / 1024d:0.0} GB" : $"{mb:0} MB";
    }
}

/// <summary>
/// Thread-safe in-memory log buffer. Each line is timestamped when it is added, so a
/// batch written later still shows when every event actually happened.
/// </summary>
public sealed class LogBuffer
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

    public void Add(string message)
    {
        _lines.Enqueue($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }

    public IReadOnlyList<string> Drain()
    {
        var drained = new List<string>(_lines.Count);
        while (_lines.TryDequeue(out var line))
        {
            drained.Add(line);
        }

        return drained;
    }
}
