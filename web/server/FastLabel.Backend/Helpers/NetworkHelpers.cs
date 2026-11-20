namespace FastLabel.Backend.Helpers;

public static class NetworkHelpers
{
    public static bool IsValidIpAddress(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        if (!IPAddress.TryParse(ip.Trim(), out var parsedIp)) return false;

        return parsedIp.AddressFamily == AddressFamily.InterNetwork
            ? ip.Trim().Count(c => c == '.') == 3
            : parsedIp.AddressFamily == AddressFamily.InterNetworkV6;
    }
}