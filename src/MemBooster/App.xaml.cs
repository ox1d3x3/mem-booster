using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace MemBooster;

public partial class App : Application
{
    private const long MaxStartupLogBytes = 1024L * 1024L;
    private const string AppVersion = "0.5.20";

    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Mem-Booster");

    public static string StartupLogPath => Path.Combine(AppDataDirectory, "logs", "startup.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterGlobalExceptionHandlers();
        base.OnStartup(e);
    }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            WriteStartupLog($"Application startup requested. version={AppVersion}");
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            WriteStartupLog("Main window shown successfully.");
        }
        catch (Exception ex)
        {
            HandleFatalStartupException(ex);
        }
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                WriteStartupLog("Unhandled domain exception", ex);
            }
            else
            {
                WriteStartupLog($"Unhandled domain exception: {args.ExceptionObject}");
            }
        };

        Current.DispatcherUnhandledException += (_, args) =>
        {
            WriteStartupLog("Unhandled UI exception", args.Exception);
            MessageBox.Show(
                "Mem-Booster hit an unexpected UI error.\n\n" +
                args.Exception.Message +
                "\n\nA diagnostic log was written here:\n" + StartupLogPath,
                "Mem-Booster Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteStartupLog("Unhandled task exception", args.Exception);
            args.SetObserved();
        };
    }

    private static void HandleFatalStartupException(Exception ex)
    {
        WriteStartupLog("Fatal startup exception", ex);
        MessageBox.Show(
            "Mem-Booster could not start.\n\n" +
            ex.Message +
            "\n\nA diagnostic log was written here:\n" + StartupLogPath,
            "Mem-Booster Startup Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Current.Shutdown(1);
    }

    public static void WriteStartupLog(string message, Exception? exception = null)
    {
        try
        {
            var logDirectory = Path.GetDirectoryName(StartupLogPath);
            if (!string.IsNullOrWhiteSpace(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            var builder = new StringBuilder();
            builder.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ").AppendLine(message);
            if (exception is not null)
            {
                builder.AppendLine(exception.ToString());
            }

            RotateStartupLogIfNeeded();
            File.AppendAllText(StartupLogPath, builder.ToString());
        }
        catch
        {
            // Logging must never stop the app from opening.
        }
    }
    private static void RotateStartupLogIfNeeded()
    {
        try
        {
            if (!File.Exists(StartupLogPath))
            {
                return;
            }

            var info = new FileInfo(StartupLogPath);
            if (info.Length < MaxStartupLogBytes)
            {
                return;
            }

            var archivePath = StartupLogPath + ".old";
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            File.Move(StartupLogPath, archivePath);
        }
        catch
        {
            // Startup log rotation must never stop the app from opening.
        }
    }

}
