using FamilyTheater.Core.Enum;
using System;
using System.IO;
using System.Text;

namespace FamilyTheater.Core.Logger;

public static class Logger
{
    private static readonly object SyncRoot = new();
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private static string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FamilyTheater",
        "logs");

    private static LogLevel _minimumLevel = LogLevel.DEBUG;

    public static string LogDirectory => _logDirectory;

    public static LogLevel MinimumLevel => _minimumLevel;

    public static void Configure(string? logDirectory = null, LogLevel minimumLevel = LogLevel.DEBUG)
    {
        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
            _logDirectory = logDirectory;
        }

        _minimumLevel = minimumLevel;
        EnsureLogDirectory();
    }

    public static void Debug(string message, Exception? exception = null)
        => Write(LogLevel.DEBUG, message, exception);

    public static void Info(string message, Exception? exception = null)
        => Write(LogLevel.INFO, message, exception);

    public static void Warn(string message, Exception? exception = null)
        => Write(LogLevel.WARN, message, exception);

    public static void Error(string message, Exception? exception = null)
        => Write(LogLevel.ERROR, message, exception);

    public static void Fatal(string message, Exception? exception = null)
        => Write(LogLevel.FATAL, message, exception);

    public static void Log(LogLevel level, string message, Exception? exception = null)
        => Write(level, message, exception);

    public static void Write(LogLevel level, string message, Exception? exception = null)
    {
        if (level < _minimumLevel)
            return;

        try
        {
            EnsureLogDirectory();

            var logFilePath = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var logText = BuildLogText(level, message, exception);

            lock (SyncRoot)
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

    private static void EnsureLogDirectory()
    {
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }
}
