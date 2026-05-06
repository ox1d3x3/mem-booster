using System.IO;

namespace MemBooster.Services;

public sealed class LoggerService
{
    private readonly string _logDirectory;

    public LoggerService(string appDataDirectory)
    {
        _logDirectory = Path.Combine(appDataDirectory, "logs");
    }

    public string LogPath => Path.Combine(_logDirectory, "boost.log");

    public void Write(string message)
    {
        Directory.CreateDirectory(_logDirectory);
        File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }

    public void WriteBoost(string selected, BoostResult result)
    {
        Write($"Boost profile=[{selected}] => {result.Summary}");
        foreach (var line in result.Messages.Take(25))
        {
            Write($"  {line}");
        }
    }
}
