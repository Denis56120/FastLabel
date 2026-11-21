[Setup]
AppName=FastLabel Agent
AppVersion=1.0.0
VersionInfoVersion=1.0.0.0
VersionInfoCopyright=Copyright © 2026 FastLabel
DefaultDirName={autopf}\FastLabel
DefaultGroupName=FastLabel
OutputDir=.
OutputBaseFilename=setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ShowLanguageDialog=yes

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
french.RegisterService=Enregistrement du service FastLabel...
english.RegisterService=Registering FastLabel service...
french.StartService=Démarrage du service FastLabel...
english.StartService=Starting FastLabel service...
french.StopService=Arrêt du service FastLabel...
english.StopService=Stopping FastLabel service...
french.UninstallService=Désinstallation du service FastLabel...
english.UninstallService=Uninstalling FastLabel service...

[Files]
; Copy the single service executable into the installation directory
Source: "..\FastLabel.Service\bin\Release\net10.0\win-x64\publish\FastLabel.Service.exe"; DestDir: "{app}"; Flags: ignoreversion

[Run]
; 1. Register the Windows service
Filename: "{app}\FastLabel.Service.exe"; Parameters: "install"; StatusMsg: "{cm:RegisterService}"; Flags: runhidden

; 2. Start the service (which will handle the rest)
Filename: "sc"; Parameters: "start FastLabelService"; StatusMsg: "{cm:StartService}"; Flags: runhidden

[UninstallRun]
; 1. Stop the service cleanly before uninstallation
Filename: "sc"; Parameters: "stop FastLabelService"; StatusMsg: "{cm:StopService}"; Flags: runhidden; RunOnceId: "StopFastLabelService"

; 2. Uninstall and remove the service from the Windows Service Manager
Filename: "{app}\FastLabel.Service.exe"; Parameters: "uninstall"; StatusMsg: "{cm:UninstallService}"; Flags: runhidden; RunOnceId: "UninstallFastLabelService"

[UninstallDelete]
; Remove the entire installation directory and its contents (log files, subdirectories, etc.)
Type: filesandordirs; Name: "{app}"