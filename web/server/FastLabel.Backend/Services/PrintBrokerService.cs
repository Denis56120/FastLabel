using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace FastLabel.Backend.Services;

public class PrintBrokerService
{
    // Dictionnaires partagés pour la gestion des WebSockets et du verrouillage des jobs
    public ConcurrentDictionary<int, WebSocket> ActiveAgents { get; } = new();
    public ConcurrentDictionary<string, TaskCompletionSource<(string status, string message)>> PendingJobs { get; } = new();
}