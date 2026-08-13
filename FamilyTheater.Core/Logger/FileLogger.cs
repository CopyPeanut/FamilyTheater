using FamilyTheater.Core.Enum;
using System.Text;

namespace FamilyTheater.Core.Logger;

public class FileLogger : IAppLogger
{
    private readonly object _syncRoot = new();
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private readonly string _logDirectory;
    private readonly LogLevel _minimumLevel;

    public FileLogger(string logDirectory, LogLevel minimumLevel = LogLevel.DEBUG)
    {
        _logDirectory = logDirectory;
        _minimumLevel = minimumLevel;
        EnsureLogDirectory();
    }

    public void Debug(string message, Exception? exception = null)
        => Write(LogLevel.DEBUG, message, exception);

    public void Info(string message, Exception? exception = null)
        => Write(LogLevel.INFO, message, exception);

    public void Warn(string message, Exception? exception = null)
        => Write(LogLevel.WARN, message, exception);

    public void Error(string message, Exception? exception = null)
        => Write(LogLevel.ERROR, message, exception);

    public void Fatal(string message, Exception? exception = null)
        => Write(LogLevel.FATAL, message, exception);

    public void Log(LogLevel level, string message, Exception? exception = null)
        => Write(level, message, exception);

    private void Write(LogLevel level, string message, Exception? exception)
    {
        if (level < _minimumLevel)
            return;

        try
        {
            EnsureLogDirectory();

            var logFilePath = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var logText = BuildLogText(level, message, exception);

            lock (_syncRoot)
            {
                File.AppendAllText(logFilePath, logText, Utf8NoBom);
            }
        }
        catch
        {
            // Logging should never interrupt the main application flow.
        }
    }

    private static string BuildLogText(LogLevel level, string message, Exception? exception)
    {
        var builder = new StringBuilder();

        builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.Append(" [");
        builder.Append(level);
        builder.Append("] ");
        builder.AppendLine(message);

        if (exception != null)
        {
            builder.AppendLine(exception.ToString());
        }

        return builder.ToString();
    }

    private void EnsureLogDirectory()
    {
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }
}
