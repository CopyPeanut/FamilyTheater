using FamilyTheater.Core.Enum;

namespace FamilyTheater.Core.Logger;

public interface IAppLogger
{
    void Debug(string message, Exception? exception = null);

    void Info(string message, Exception? exception = null);

    void Warn(string message, Exception? exception = null);

    void Error(string message, Exception? exception = null);

    void Fatal(string message, Exception? exception = null);

    void Log(LogLevel level, string message, Exception? exception = null);
}
