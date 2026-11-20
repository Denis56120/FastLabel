namespace FastLabel.Client.Helpers;

public static class AppLog
{
    private static ILoggerFactory? _factory;

    public static void Configure(ILoggerFactory factory)
    {
        _factory = factory;
    }

    public static void Log(
        string? message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        var category = Path.GetFileNameWithoutExtension(filePath);
        var logger = _factory?.CreateLogger(string.IsNullOrEmpty(category) ? "FastLabel.Client" : category);

        if (logger is null)
        {
            Console.WriteLine($"[{category}.{memberName}] {message}");
            return;
        }

        logger.LogDebug("{Message} ({Member})", message ?? "Erreur inattendue", memberName);
    }

    public static void Error(
        Exception ex,
        string? message = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        var category = Path.GetFileNameWithoutExtension(filePath);
        var logger = _factory?.CreateLogger(string.IsNullOrEmpty(category) ? "FastLabel.Client" : category);

        if (logger is null)
        {
            Console.Error.WriteLine($"[{category}.{memberName}] {message ?? ex.Message}: {ex}");
            return;
        }

        logger.LogError(ex, "{Message} ({Member})", message ?? "Erreur inattendue", memberName);
    }

    public static void Warn(
        Exception ex,
        string? message = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        var category = Path.GetFileNameWithoutExtension(filePath);
        var logger = _factory?.CreateLogger(string.IsNullOrEmpty(category) ? "FastLabel.Client" : category);

        if (logger is null)
        {
            Console.WriteLine($"[{category}.{memberName}] {message ?? ex.Message}: {ex}");
            return;
        }

        logger.LogWarning(ex, "{Message} ({Member})", message ?? "Avertissement", memberName);
    }
}