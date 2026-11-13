namespace FastLabel.Backend.Services;

public class PrintBrokerService
{
    public ConcurrentDictionary<int, WebSocket> ActiveAgents { get; } = new();
    public ConcurrentDictionary<string, TaskCompletionSource<(string status, string message)>> PendingJobs { get; } = new();
}