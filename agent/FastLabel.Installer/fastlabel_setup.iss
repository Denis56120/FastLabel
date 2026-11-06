[Setup]
AppName=FastLabel Printing Agent
AppVersion=1.0.0
DefaultDirName={autopf}\FastLabel
DefaultGroupName=FastLabel
OutputDir=.
OutputBaseFilename=FastLabel_Setup_v1.0.0
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Files]
; Copie l'unique fichier exécutable du service dans le dossier d'installation
Source: "..\FastLabel.Service\bin\Release\net10.0\win-x64\publish\FastLabel.Service.exe"; DestDir: "{app}"; Flags: ignoreversion

[Run]
; 1. Enregistre le service Windows
Filename: "{app}\FastLabel.Service.exe"; Parameters: "install"; StatusMsg: "Enregistrement du service FastLabel..."; Flags: runhidden

; 2. Démarre le service (qui gérera le reste)
Filename: "sc"; Parameters: "start FastLabelService"; StatusMsg: "Démarrage du service FastLabel..."; Flags: runhidden

[UninstallRun]
; 1. Arrête le service proprement avant la désinstallation
Filename: "sc"; Parameters: "stop FastLabelService"; Flags: runhidden

; 2. Désinstalle et supprime le service du gestionnaire Windows
Filename: "{app}\FastLabel.Service.exe"; Parameters: "uninstall"; Flags: runhidden

[UninstallDelete]
; Supprime l'intégralité du dossier d'installation et son contenu (fichiers de logs, sous-dossiers, etc.)
Type: filesandordirs; Name: "{app}"