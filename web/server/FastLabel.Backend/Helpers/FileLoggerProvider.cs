namespace FastLabel.Backend.Helpers;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly LogLevel _minLevel;
    private readonly object _lock = new();

    public FileLoggerProvider(string logDirectory, LogLevel minLevel = LogLevel.Information)
    {
        _logDirectory = logDirectory;
        _minLevel = minLevel;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _logDirectory, _minLevel, _lock);
    }

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logDirectory;
        private readonly LogLevel _minLevel;
        private readonly object _lock;

        public FileLogger(string categoryName, string logDirectory, LogLevel minLevel, object writeLock)
        {
            _categoryName = categoryName;
            _logDirectory = logDirectory;
            _minLevel = minLevel;
            _lock = writeLock;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception == null) return;

            var now = DateTime.UtcNow;
            var filePath = Path.Combine(_logDirectory, $"fastlabel-{now:yyyy-MM-dd}.log");

            var sb = new StringBuilder();
            sb.AppendLine($"[{now:yyyy-MM-dd HH:mm:ss.fff} UTC] [{logLevel}] [{_categoryName}] {message}");
            if (exception != null)
            {
                sb.AppendLine(exception.ToString());
            }

            lock (_lock)
            {
                File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
        }
    }
}