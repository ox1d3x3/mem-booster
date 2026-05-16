using Microsoft.UI.Xaml;
using MemBooster.Services;

namespace MemBooster;

public partial class App : Application
{
    private Window? _window;
    private readonly string _appDataDirectory;
    private readonly string _startupLogPath;

    public App()
    {
        _appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mem-Booster");
        Directory.CreateDirectory(_appDataDirectory);

        var logDirectory = Path.Combine(_appDataDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        _startupLogPath = Path.Combine(logDirectory, "startup.log");

        WriteStartupLog("Application startup requested. version=0.6.13 WinUI3");
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
            WriteStartupLog("Main window shown successfully.");
        }
        catch (Exception ex)
        {
            WriteStartupLog("Fatal startup exception" + Environment.NewLine + ex);
            throw;
        }
    }

    private void WriteStartupLog(string message)
    {
        try
        {
            File.AppendAllText(
                _startupLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Startup logging must never prevent app launch.
        }
    }
}
