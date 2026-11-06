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
    private NotifyIcon _trayIcon;
    private ContextMenuStrip _contextMenu;
    private System.Threading.Timer? _statusTimer;

    public TrayApplicationContext()
    {
        // 1. Création du menu contextuel de la barre des tâches
        _contextMenu = new ContextMenuStrip();

        var statusItem = new ToolStripMenuItem("Statut : Service en cours...") { Enabled = false };
        var updateItem = new ToolStripMenuItem("Vérifier les mises à jour", null, OnCheckUpdateClick);
        var exitItem = new ToolStripMenuItem("Quitter", null, OnExitClick);

        _contextMenu.Items.Add(statusItem);
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

        // 3. Optionnel : un timer pour interroger périodiquement le service local (ou l'état d'impression)
        _statusTimer = new System.Threading.Timer(CheckServiceStatus, null, 2000, 10000);
    }

    private void CheckServiceStatus(object? state)
    {
        // TODO: Vérifier ici si le service Windows répond ou si des impressions sont en cours
        // et mettre à jour le texte du tooltip (_trayIcon.Text) ou l'icône si besoin.
    }

    private async void OnCheckUpdateClick(object? sender, EventArgs e)
    {
        try
        {
            // Désactiver temporairement le clic pour éviter les doublons
            if (sender is ToolStripMenuItem item) item.Enabled = false;

            MessageBox.Show("Vérification des mises à jour auprès du service...", "FastLabel", MessageBoxButtons.OK, MessageBoxIcon.Information);

            using var client = new NamedPipeClientStream(".", "FastLabelUpdatePipe", PipeDirection.Out);
            await client.ConnectAsync(3000); // Timeout de 3 secondes

            using var writer = new StreamWriter(client, Encoding.UTF8);
            await writer.WriteAsync("TRIGGER_UPDATE");
            await writer.FlushAsync();
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
        // Masquer l'icône immédiatement pour l'enlever de la barre des tâches
        _trayIcon.Visible = false;
        _statusTimer?.Dispose();
        Application.Exit();
    }
}