using System.IO.Pipes;
using System.Text;

namespace FastLabel.Tray;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext());
    }
}

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly System.Threading.Timer? _statusTimer;

    public TrayApplicationContext()
    {
        // 1. Création du menu contextuel de la barre des tâches
        _contextMenu = new ContextMenuStrip();

        // On initialise avec un texte temporaire
        var statusItem = new ToolStripMenuItem("Statut : Chargement...");

        // On initialise avec un texte temporaire
        var versionItem = new ToolStripMenuItem("Version : Chargement...");

        var updateItem = new ToolStripMenuItem("Vérifier les mises à jour", null, OnCheckUpdateClick);
        var exitItem = new ToolStripMenuItem("Quitter", null, OnExitClick);

        _contextMenu.Items.Add(statusItem);
        _contextMenu.Items.Add(versionItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(updateItem);
        _contextMenu.Items.Add(exitItem);

        // 2. Initialisation de l'icône Tray        
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FastLabel.Tray.ico");
        _trayIcon = new NotifyIcon()
        {
            Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application,
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Text = "FastLabel - Agent d'Impression"
        };

        _ = LoadVersionAsync(versionItem);
        _ = LoadStatusAsync(statusItem);
    }


    private async Task UpdateMenuItemAsync(ToolStripMenuItem menuItem, string prefix, Func<Task<string>> fetchAction)
    {
        string result = await fetchAction();

        var updateAction = new Action(() => menuItem.Text = prefix + result);

        if (_contextMenu.InvokeRequired)
        {
            _contextMenu.Invoke(updateAction);
        }
        else
        {
            updateAction();
        }
    }

    private Task LoadStatusAsync(ToolStripMenuItem statusItem) =>
        UpdateMenuItemAsync(statusItem, "Statut : ", RequestStatusCheckFromTrayAsync);

    private Task LoadVersionAsync(ToolStripMenuItem versionItem) =>
        UpdateMenuItemAsync(versionItem, "Version : ", RequestVersionCheckFromTrayAsync);

    private static async Task<string> SendPipeCommandAsync(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", "FastLabelUpdatePipe", PipeDirection.InOut);
            await client.ConnectAsync(2000);

            // 1. Envoyer la commande au service
            using (var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true))
            {
                await writer.WriteLineAsync(command);
                await writer.FlushAsync();
            }

            // 2. Lire la réponse du service
            using var reader = new StreamReader(client, Encoding.UTF8);
            string? response = await reader.ReadLineAsync();

            return response ?? "NoResponse";
        }
        catch (Exception)
        {
            return "Error";
        }
    }

    private static Task<string> RequestStatusCheckFromTrayAsync() => SendPipeCommandAsync("CHECK_STATUS");

    private static Task<string> RequestVersionCheckFromTrayAsync() => SendPipeCommandAsync("CHECK_VERSION");

    private async void OnCheckUpdateClick(object? sender, EventArgs e)
    {
        try
        {
            // Désactiver temporairement le clic pour éviter les doublons
            if (sender is ToolStripMenuItem item) item.Enabled = false;

            MessageBox.Show("Vérification des mises à jour auprès du service...", "FastLabel", MessageBoxButtons.OK, MessageBoxIcon.Information);

            string response = await SendPipeCommandAsync("TRIGGER_UPDATE");

            if (response == "Error" || response == "NoResponse")
            {
                MessageBox.Show("Impossible de contacter le service local pour la mise à jour.", "FastLabel Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception)
        {
            MessageBox.Show("Impossible de contacter le service local pour la mise à jour.", "FastLabel Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (sender is ToolStripMenuItem item) item.Enabled = true;
        }
    }

    private void OnExitClick(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _statusTimer?.Dispose();
        Application.Exit();
    }
}