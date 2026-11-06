using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

public static class NetworkPrinterHelper
{
    public static async Task<bool> SendDataToIpAsync(string ipAddress, int port, string data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(data);
        return await SendBytesToIpAsync(ipAddress, port, bytes);
    }

    public static async Task<bool> SendBytesToIpAsync(string ipAddress, int port, byte[] bytes)
    {
        using var client = new TcpClient();
        try
        {
            var connectTask = client.ConnectAsync(ipAddress, port);
            if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
            {
                return false; // Timeout de connexion
            }

            using (var stream = client.GetStream())
            {
                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            } 

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            // Fermeture explicite de la socket pour être absolument sûr
            client.Close();
        }
    }
}