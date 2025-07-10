using System;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using VpnClient.Core.Models;

namespace VpnClient.Infrastructure.Logging;

public class ObservableLoggerProvider : ILoggerProvider
{
    private readonly ObservableCollection<LogEntry> _entries;

    public ObservableLoggerProvider(ObservableCollection<LogEntry> entries)
    {
        _entries = entries;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ObservableLogger(_entries);
    }

    public void Dispose()
    {
    }

    private sealed class ObservableLogger : ILogger
    {
        private readonly ObservableCollection<LogEntry> _entries;

        public ObservableLogger(ObservableCollection<LogEntry> entries)
        {
            _entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _entries.Add(new LogEntry(DateTime.Now, message));
        }
    }
}
