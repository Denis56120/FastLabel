namespace FastLabel.Backend.Helpers;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly IReadOnlyDictionary<string, LogLevel> _rules;
    private readonly LogLevel _defaultLevel;
    private readonly object _lock = new();

    public FileLoggerProvider(string logDirectory, IConfiguration? configuration = null, LogLevel fallbackDefaultLevel = LogLevel.Warning)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);

        var rules = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase);
        var section = configuration?.GetSection("Logging:LogLevel");
        if (section is not null)
        {
            foreach (var child in section.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Value) && Enum.TryParse<LogLevel>(child.Value, true, out var parsedLevel))
                {
                    rules[child.Key] = parsedLevel;
                }
            }
        }

        _defaultLevel = rules.TryGetValue("Default", out var defaultLevel) ? defaultLevel : fallbackDefaultLevel;
        rules.Remove("Default");
        _rules = rules;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var minLevel = ResolveMinLevel(categoryName);
        return new FileLogger(categoryName, _logDirectory, minLevel, _lock);
    }

    public void Dispose()
    {
    }

    private LogLevel ResolveMinLevel(string categoryName)
    {
        string? matchedKey = null;
        LogLevel matchedLevel = _defaultLevel;

        foreach (var rule in _rules)
        {
            if (categoryName.StartsWith(rule.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (matchedKey is null || rule.Key.Length > matchedKey.Length)
                {
                    matchedKey = rule.Key;
                    matchedLevel = rule.Value;
                }
            }
        }

        return matchedLevel;
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