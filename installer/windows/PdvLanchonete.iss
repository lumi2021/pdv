; Instalador do PDV Lanchonete para Windows (Inno Setup 6).
; Uso: ISCC /DAppVersion=1.0.0 installer\windows\PdvLanchonete.iss   (a pasta "publicado" vem do dotnet publish)

#define AppName "PDV Lanchonete"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{B6D1E9C2-5E55-4C2B-9F57-6E8E0A1D7A31}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={autopf}\PdvLanchonete
DisableProgramGroupPage=yes
OutputDir=..\..\saida
OutputBaseFilename=PdvLanchonete-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\..\src\Pdv.Desktop\Assets\icone.ico
UninstallDisplayIcon={app}\PdvLanchonete.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Instala só para o usuário, sem pedir senha de administrador. Quem escolher "para todos" libera o celular no firewall.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "atalhodesktop"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos:"

[Files]
Source: "..\..\publicado\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\PdvLanchonete.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\PdvLanchonete.exe"; Tasks: atalhodesktop

[Run]
; Só com instalação para todos os usuários (administrador): libera o celular na rede privada (Wi-Fi da lanchonete).
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""PDV Lanchonete"" dir=in action=allow program=""{app}\PdvLanchonete.exe"" profile=private enable=yes"; Flags: runhidden; Check: IsAdminInstallMode; StatusMsg: "Liberando o celular na rede..."
Filename: "{app}\PdvLanchonete.exe"; Description: "Abrir o PDV Lanchonete"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""PDV Lanchonete"" program=""{app}\PdvLanchonete.exe"""; Flags: runhidden; RunOnceId: "RemoverRegraDoFirewall"

; Os dados (pedidos, estoque, cópias de segurança) ficam em %LocalAppData%\PdvLanchonete e NÃO são apagados ao desinstalar.
