using Microsoft.Extensions.Logging;
using System.Text;

namespace Chats.BE.UnitTest.Common;

internal class StringLogger<T>(LogLevel logLevel = LogLevel.Trace) : ILogger<T> where T : class
{
    private readonly StringBuilder _logBuilder = new();
    private readonly LogLevel _logLevel = logLevel;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // Scope functionality can be implemented if needed, 
        // but for simplicity, we're just returning a no-op disposable.
        return new NoopDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _logLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        if (formatter == null) throw new ArgumentNullException(nameof(formatter));

        string message = formatter(state, exception);
        _logBuilder.AppendLine($"[{logLevel}] {message}");

        if (exception != null)
        {
            _logBuilder.AppendLine(exception.ToString());
        }
    }

    public string GetLogs()
    {
        return _logBuilder.ToString();
    }

    private class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
            // No-op
        }
    }
}