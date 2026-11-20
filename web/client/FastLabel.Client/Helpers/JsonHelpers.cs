namespace FastLabel.Client.Helpers;

public static class JsonHelpers
{
    public static int GetInt32(System.Text.Json.JsonElement element, string propertyName)
    {
        return element.ValueKind == System.Text.Json.JsonValueKind.Object && element.TryGetProperty(propertyName, out var prop) && prop.TryGetInt32(out var val)
            ? val
            : 0;
    }

    public static long GetInt64(System.Text.Json.JsonElement element, string propertyName)
    {
        return element.ValueKind == System.Text.Json.JsonValueKind.Object && element.TryGetProperty(propertyName, out var prop) && prop.TryGetInt64(out var val)
            ? val
            : 0L;
    }

    public static bool GetBool(System.Text.Json.JsonElement element, string propertyName)
    {
        return element.ValueKind == System.Text.Json.JsonValueKind.Object && element.TryGetProperty(propertyName, out var prop)
            && (prop.ValueKind == System.Text.Json.JsonValueKind.True || prop.ValueKind == System.Text.Json.JsonValueKind.False)
            && prop.GetBoolean();
    }
}