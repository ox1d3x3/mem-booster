using System.Diagnostics;
using System.IO;
using System.Text;
using MemBooster.Models;

namespace MemBooster.Services;

public sealed class LoggerService
{
    private const long MaxLogBytes = 5L * 1024L * 1024L;
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

    public LogOperationScope BeginOperation(string operationName, string? metadata = null)
    {
        return new LogOperationScope(this, operationName, metadata);
    }

    public void WriteBoost(string selected, BoostResult result)
    {
        var summary = $"Boost profile=[{selected}] => {result.Summary}";
        Write(summary);
        WriteLine(BoostLogPath, summary);

        foreach (var line in result.Messages.Take(400))
        {
            WriteLine(BoostLogPath, "  " + line);
        }
    }

    public void WriteRestoreCapture(IReadOnlyList<RestoreEntry> entries)
    {
        var header = $"Restore capture entries={entries.Count}";
        Write(header);
        WriteLine(BoostLogPath, header);

        foreach (var entry in entries.Take(500))
        {
            WriteLine(BoostLogPath, $"  capture: {entry.DisplayName} | {entry.ExeName} | {entry.FilePath}");
        }
    }

    public void WriteRestoreResult(IReadOnlyList<RestoreEntry> entries, RestoreResult result)
    {
        var header = $"Restore requested entries={entries.Count} => {result.Summary}";
        Write(header);
        WriteLine(BoostLogPath, header);

        foreach (var line in result.Messages.Take(400))
        {
            WriteLine(BoostLogPath, "  " + line);
        }
    }

    public void WriteSelection(string mode, IReadOnlyCollection<string> selectedNames, IEnumerable<ProcessGroup> processGroups)
    {
        var selected = selectedNames
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var running = processGroups
            .Where(p => selected.Contains(p.ExeName, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(p => p.WorkingSetBytes)
            .ToList();

        var estimatedRam = running.Sum(p => p.WorkingSetBytes);
        Write($"Selection mode={mode}; selected={selected.Count}; runningMatches={running.Count}; estimatedWorkingSet={FormatBytes(estimatedRam)}");
        foreach (var process in running.Take(250))
        {
            Write($"  selected-running: {process.DisplayName} | {process.ExeName} | {process.MemoryText} | instances={process.InstanceCount} | status={process.RiskLabel}");
        }
    }

    public string WriteProcessSnapshot(string label, IEnumerable<ProcessGroup> processGroups, IReadOnlyCollection<string>? selectedNames = null)
    {
        Directory.CreateDirectory(SnapshotDirectory);
        var safeLabel = string.Concat(label.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-'));
        var path = Path.Combine(SnapshotDirectory, $"{DateTime.Now:yyyyMMdd-HHmmssfff}-{safeLabel}.csv");
        var selected = selectedNames ?? Array.Empty<string>();

        var builder = new StringBuilder();
        builder.AppendLine("Timestamp,Label,Selected,CanSelect,DisplayName,ExeName,WorkingSetBytes,WorkingSetText,Instances,Status,Description");
        foreach (var process in processGroups.OrderByDescending(p => p.WorkingSetBytes).ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(DateTime.Now.ToString("O")),
                Csv(label),
                Csv((process.IsSelected || selected.Contains(process.ExeName, StringComparer.OrdinalIgnoreCase)).ToString()),
                Csv(process.CanSelect.ToString()),
                Csv(process.DisplayName),
                Csv(process.ExeName),
                Csv(process.WorkingSetBytes.ToString()),
                Csv(process.MemoryText),
                Csv(process.InstanceCount.ToString()),
                Csv(process.RiskLabel),
                Csv(process.RiskDescription)
            }));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        Write($"Process snapshot saved: {path}");
        return path;
    }

    internal void WriteOperationEvent(string operationId, string operationName, string phase, TimeSpan elapsed, string? message = null)
    {
        var extra = string.IsNullOrWhiteSpace(message) ? string.Empty : $" | {message}";
        var line = $"operationId={operationId}; operation={operationName}; phase={phase}; elapsedMs={elapsed.TotalMilliseconds:0}{extra}";
        Write(line);
        WritePerformance(line);
    }

    private void WriteLine(string path, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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

public sealed class LogOperationScope : IDisposable
{
    private readonly LoggerService _logger;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _disposed;

    public string OperationId { get; } = Guid.NewGuid().ToString("N")[..8];
    public string OperationName { get; }

    public LogOperationScope(LoggerService logger, string operationName, string? metadata)
    {
        _logger = logger;
        OperationName = operationName;
        _logger.WriteOperationEvent(OperationId, OperationName, "START", TimeSpan.Zero, metadata);
    }

    public void Checkpoint(string message)
    {
        _logger.WriteOperationEvent(OperationId, OperationName, "CHECKPOINT", _stopwatch.Elapsed, message);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopwatch.Stop();
        _logger.WriteOperationEvent(OperationId, OperationName, "END", _stopwatch.Elapsed);
    }
}
