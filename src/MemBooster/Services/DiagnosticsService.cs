using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace MemBooster.Services;

public sealed class DiagnosticsService
{
    public string Collect(string appDataDirectory, string version)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
        {
            desktop = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(desktop, $"Mem-Booster-diagnostics-{timestamp}.zip");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"Mem-Booster-diagnostics-{timestamp}");

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }

        Directory.CreateDirectory(tempRoot);

        try
        {
            WriteAppInfo(tempRoot, version, appDataDirectory);
            WriteRunningProcesses(tempRoot);
            CopyIfExists(Path.Combine(appDataDirectory, "local-profile.xml"), Path.Combine(tempRoot, "local-profile.xml"));
            CopyIfExists(Path.Combine(appDataDirectory, "settings.xml"), Path.Combine(tempRoot, "settings.xml"));
            CopyIfExists(Path.Combine(appDataDirectory, "device-optimise-state.xml"), Path.Combine(tempRoot, "device-optimise-state.xml"));
            CopyDirectoryIfExists(Path.Combine(appDataDirectory, "logs"), Path.Combine(tempRoot, "logs"));

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(tempRoot, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return zipPath;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Temporary diagnostic folder cleanup is best effort only.
            }
        }
    }

    private static void WriteAppInfo(string outputDirectory, string version, string appDataDirectory)
    {
        var text = new StringBuilder();
        text.AppendLine("Mem-Booster Diagnostics");
        text.AppendLine($"Collected local time: {DateTime.Now:O}");
        text.AppendLine($"Collected UTC: {DateTime.UtcNow:O}");
        text.AppendLine($"Version: {version}");
        text.AppendLine($"Process elevated: {AdminService.IsCurrentProcessElevated()}");
        text.AppendLine($"OS: {Environment.OSVersion}");
        text.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        text.AppendLine($"64-bit process: {Environment.Is64BitProcess}");
        text.AppendLine($"Machine: {Environment.MachineName}");
        text.AppendLine($"User: {Environment.UserName}");
        text.AppendLine($"AppData: {appDataDirectory}");
        text.AppendLine("Logs: " + Path.Combine(appDataDirectory, "logs"));
        text.AppendLine($"Base directory: {AppContext.BaseDirectory}");
        File.WriteAllText(Path.Combine(outputDirectory, "app-info.txt"), text.ToString(), Encoding.UTF8);
    }

    private static void WriteRunningProcesses(string outputDirectory)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Id,ProcessName,ExeName,WorkingSetMB,MainWindowTitle,Path");

        foreach (var process in Process.GetProcesses().OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var processName = process.ProcessName;
                var exeName = SafetyRules.NormaliseProcessName(processName);
                var workingSetMb = process.WorkingSet64 / 1024d / 1024d;
                var title = Safe(() => process.MainWindowTitle) ?? string.Empty;
                var path = Safe(() => process.MainModule?.FileName) ?? string.Empty;

                csv.AppendLine(string.Join(",", new[]
                {
                    process.Id.ToString(),
                    Csv(processName),
                    Csv(exeName),
                    workingSetMb.ToString("0.0"),
                    Csv(title),
                    Csv(path)
                }));
            }
            catch
            {
                // Processes can exit while being read. Skip unstable rows.
            }
            finally
            {
                process.Dispose();
            }
        }

        File.WriteAllText(Path.Combine(outputDirectory, "running-processes.csv"), csv.ToString(), Encoding.UTF8);
    }

    private static string? Safe(Func<string?> reader)
    {
        try
        {
            return reader();
        }
        catch
        {
            return null;
        }
    }

    private static string Csv(string value)
    {
        value ??= string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static void CopyDirectoryIfExists(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(destinationDirectory);
        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destinationPath = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }
}
