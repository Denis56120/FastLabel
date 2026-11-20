namespace FastLabel.Backend.Helpers;

public static class FileHelpers
{
    public static string GetFile(string filename)
    {
        var normalizedTarget = filename.Replace('\\', '.').Replace('/', '.');
        var assembly = typeof(AppDbContext).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            var available = string.Join(", ", assembly.GetManifestResourceNames());
            AppLog.Error(new InvalidOperationException($"Embedded resource not found: {filename}. Available: {available}"));
            return string.Empty;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            AppLog.Error(new InvalidOperationException($"Failed to open embedded resource stream: {resourceName}"));
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}