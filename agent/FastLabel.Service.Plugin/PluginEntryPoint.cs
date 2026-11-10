using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FastLabel.Core;

namespace FastLabel.Service.Plugin;

public class PluginEntryPoint : IFastLabelPlugin
{
    private CancellationTokenSource? _cts;

    public void Launch(string baseUrl)
    {
        _cts = new CancellationTokenSource();

        Task.Run(() => ConnectToCloudWebSocketAsync(baseUrl, _cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private static async Task ConnectToCloudWebSocketAsync(string baseUrl, CancellationToken token)
    {
        string wsUrl = $"wss://{baseUrl}/agent-ws";

        while (!token.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(new Uri(wsUrl), token);

                byte[] buffer = new byte[8192];

                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Fermeture demandée par le serveur", token);
                        break;
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        bool success = false;
                        string responseMessage = "Impression échouée";

                        try
                        {
                            // Structure JSON attendue en provenance du site : { "ip": "192.168.1.50", "port": 9100, "data": "^XA...^XZ" }
                            using var doc = JsonDocument.Parse(jsonMessage);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("ip", out var ipProp) && 
                                root.TryGetProperty("port", out var portProp) && 
                                root.TryGetProperty("data", out var dataProp))
                            {
                                string ip = ipProp.GetString() ?? string.Empty;
                                int port = portProp.GetInt32();
                                string data = dataProp.GetString() ?? string.Empty;

                                if (!string.IsNullOrEmpty(ip) && port > 0 && !string.IsNullOrEmpty(data))
                                {
                                    // Envoi vers l'imprimante réseau locale
                                    success = await SendBytesToIpAsync(ip, port, data);
                                    responseMessage = success ? $"Imprimé avec succès sur {ip}:{port}" : $"Impossible de joindre {ip}:{port}";
                                }
                                else
                                {
                                    responseMessage = "Paramètres IP, port ou data invalides.";
                                }
                            }
                            else
                            {
                                responseMessage = "Format JSON invalide : 'ip', 'port' ou 'data' manquant.";
                            }
                        }
                        catch (Exception ex)
                        {
                            responseMessage = $"Erreur de traitement : {ex.Message}";
                        }

                        // Retourner le statut de l'impression au serveur via la même socket
                        string jsonResponse = JsonSerializer.Serialize(new { status = success ? "success" : "error", message = responseMessage });
                        byte[] responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
                        await ws.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, token);
                    }
                }
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, token);
                }
                catch (OperationCanceledException) { }
            }
        }
    }

    private static async Task<bool> SendBytesToIpAsync(string ipAddress, int port, string data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(data);

        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(3000);

        try
        {
            await client.ConnectAsync(ipAddress, port, cts.Token);

            using var stream = client.GetStream();
            await stream.WriteAsync(bytes, cts.Token);
            await stream.FlushAsync(cts.Token);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}