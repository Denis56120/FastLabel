namespace FastLabel.Backend.Helpers;

public static class AppLog
{
    private const string DefaultCategory = "FastLabel.Backend";
    private static ILoggerFactory? _factory;

    public static void Configure(ILoggerFactory factory)
    {
        _factory = factory;
    }

    public static void Error(
        Exception ex,
        string? message = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        WriteLog(LogLevel.Error, ex, message ?? "Erreur inattendue", memberName, filePath);
    }

    public static void Warn(
        Exception ex,
        string? message = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        WriteLog(LogLevel.Warning, ex, message ?? "Avertissement", memberName, filePath);
    }

    private static void WriteLog(
        LogLevel level,
        Exception ex,
        string message,
        string memberName,
        string filePath)
    {
        var category = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(category))
        {
            category = DefaultCategory;
        }

        var logger = _factory?.CreateLogger(category);
        if (logger is null)
        {
            var output = level >= LogLevel.Error ? Console.Error : Console.Out;
            output.WriteLine($"[{level}] [{category}.{memberName}] {message}: {ex}");
            return;
        }

        logger.Log(level, ex, "{Message} ({Member})", message, memberName);
    }
}