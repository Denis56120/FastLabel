using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FastLabel.Core;

namespace FastLabel.Service.Plugin;

public class PluginEntryPoint : IFastLabelPlugin
{
    private CancellationTokenSource? _cts;
    private HttpListener? _wsListener;

    public void Launch()
    {
        Console.WriteLine("[Plugin] Initialisation (Impression réseau dynamique par IP/Port)...");
        _cts = new CancellationTokenSource();

        Task.Run(() => StartWebSocketListenerAsync(_cts.Token));
    }

    public void Stop()
    {
        Console.WriteLine("[Plugin] Arrêt du serveur WebSocket et nettoyage...");
        _cts?.Cancel();
        _wsListener?.Stop();
        _cts?.Dispose();
    }

    private async Task StartWebSocketListenerAsync(CancellationToken token)
    {
        try
        {
            _wsListener = new HttpListener();
            _wsListener.Prefixes.Add("http://localhost:8181/print/");
            _wsListener.Start();
            Console.WriteLine("[Plugin WebSocket] Écoute active sur ws://localhost:8181/print/...");

            while (!token.IsCancellationRequested)
            {
                var context = await _wsListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    _ = Task.Run(() => HandleWebSocketClientAsync(context, token), token);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[Plugin WebSocket] Erreur : {ex.Message}");
        }
    }

    private async Task HandleWebSocketClientAsync(HttpListenerContext context, CancellationToken token)
    {
        WebSocketContext wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
        WebSocket webSocket = wsContext.WebSocket;

        byte[] buffer = new byte[8192];

        while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Fermeture demandée", token);
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                string jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                bool success = false;
                string responseMessage = "Impression échouée";

                try
                {
                    // Structure JSON attendue : { "ip": "192.168.1.50", "port": 9100, "data": "^XA...^XZ" }
                    using var doc = JsonDocument.Parse(jsonMessage);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("ip", out var ipProp) && 
                        root.TryGetProperty("port", out var portProp) && 
                        root.TryGetProperty("data", out var dataProp))
                    {
                        string ip = ipProp.GetString() ?? string.Empty;
                        int port = portProp.GetInt32();
                        string zplData = dataProp.GetString() ?? string.Empty;

                        if (!string.IsNullOrEmpty(ip) && port > 0 && !string.IsNullOrEmpty(zplData))
                        {
                            // Connexion éphémère, envoi, puis fermeture immédiate de la socket
                            success = await NetworkPrinterHelper.SendDataToIpAsync(ip, port, zplData);
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

                // Réponse au client WebSocket
                string jsonResponse = $"{{\"status\":\"{(success ? "success" : "error")}\",\"message\":\"{responseMessage}\"}}";
                byte[] responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
                await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, token);
            }
        }
    }
}