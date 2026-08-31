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

            var timestamp = DateTime.Now;
            var logFilePath = Path.Combine(_logDirectory, $"{timestamp:yyyy-MM-dd}.log");
            var logText = BuildLogText(timestamp, level, message, exception);

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

    private static string BuildLogText(DateTime timestamp, LogLevel level, string message, Exception? exception)
    {
        var builder = new StringBuilder();
        var prefix = $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level}] ";

        AppendLines(builder, prefix, message);

        if (exception != null)
        {
            AppendLines(builder, prefix, exception.ToString());
        }

        return builder.ToString();
    }

    private static void AppendLines(StringBuilder builder, string prefix, string text)
    {
        using var reader = new StringReader(text);
        var hasLine = false;

        while (reader.ReadLine() is { } line)
        {
            builder.Append(prefix);
            builder.AppendLine(line);
            hasLine = true;
        }

        if (!hasLine)
        {
            builder.AppendLine(prefix);
        }
    }

    private void EnsureLogDirectory()
    {
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }
}
