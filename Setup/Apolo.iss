; Instalador do Apolo (Inno Setup 6)
; O build do GitHub Actions (.NET 8) gera este instalador sozinho (veja .github/workflows/build.yml).
; Para gerar na sua maquina: compile o projeto, junte tudo na pasta "dist" (como o workflow faz)
; e abra este arquivo no Inno Setup, ou rode:  iscc /DMyAppVersion=0.71.0 Setup\Apolo.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.71.0"
#endif
#define MyAppName "Apolo"
#define MyAppPublisher "Igor Teuri (Dados Críticos)"
#define MyAppURL "https://github.com/igorteuri/Apolo"
#define DistDir "..\dist"

[Setup]
; AppId identifica o programa no Windows (instalar de novo por cima atualiza a versao). Nao mude.
AppId={{6E1D7F0A-3B2C-4C59-9D4E-7A1B5C8F2E41}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer
OutputBaseFilename=Apolo-Setup-{#MyAppVersion}
SetupIconFile=..\Ares.ico
UninstallDisplayIcon={app}\Apolo.exe
UninstallDisplayName={#MyAppName}
WizardStyle=modern
WizardImageFile=wizard-grande.bmp,wizard-grande-2x.bmp
WizardSmallImageFile=wizard-pequena.bmp,wizard-pequena-2x.bmp
LicenseFile=..\LICENSE.txt
Compression=lzma2
SolidCompression=yes
; Instala para todos os usuarios (precisa de administrador). Na primeira tela o Windows pergunta;
; quem preferir pode escolher instalar so para o usuario atual.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0.14393
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "firewall"; Description: "{cm:TaskFirewall}"; GroupDescription: "{cm:TaskGroupRemote}"; Check: IsAdminInstallMode

[Files]
; tudo o que o build gerou (programa, bibliotecas, ponte do Discord, plugin do Stream Deck, modulo do Foundry)
Source: "{#DistDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.vshost.*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Licencas\*"; DestDir: "{app}\Licencas"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\TipsOfTheDay.xml"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\TipsOfTheDay_de.xml"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\Apolo.exe"
Name: "{group}\{cm:ShortcutStreamDeck}"; Filename: "{app}\StreamDeck\Apolo.streamDeckPlugin"; Check: FileExists(ExpandConstant('{app}\StreamDeck\Apolo.streamDeckPlugin'))
Name: "{group}\{cm:ShortcutFoundry}"; Filename: "{app}\FoundryModule"; Check: DirExists(ExpandConstant('{app}\FoundryModule'))
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Apolo.exe"; Tasks: desktopicon

[Registry]
; projetos do Apolo (.apolo) abrem no Player; projetos exportados (.apkg) tambem abrem no Player
Root: HKA; Subkey: "Software\Classes\.apolo"; ValueType: string; ValueName: ""; ValueData: "ApoloProject"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\ApoloProject"; ValueType: string; ValueName: ""; ValueData: "{cm:FileTypeProject}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\ApoloProject\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Apolo.exe,0"
Root: HKA; Subkey: "Software\Classes\ApoloProject\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Apolo.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\.apkg"; ValueType: string; ValueName: ""; ValueData: "ApoloPackedProject"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\ApoloPackedProject"; ValueType: string; ValueName: ""; ValueData: "{cm:FileTypePacked}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\ApoloPackedProject\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Apolo.exe,0"
Root: HKA; Subkey: "Software\Classes\ApoloPackedProject\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Apolo.exe"" ""%1"""

[Run]
; libera a porta do controle remoto (navegador, celular, Stream Deck) no Firewall do Windows
Filename: "{app}\WinSecurity\Ares.WinSecurity.exe"; Parameters: "AddPort"; WorkingDir: "{app}\WinSecurity"; Flags: runhidden waituntilterminated; StatusMsg: "{cm:TaskFirewall}"; Tasks: firewall
Filename: "{app}\Apolo.exe"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\WinSecurity\Ares.WinSecurity.exe"; Parameters: "RemovePort"; WorkingDir: "{app}\WinSecurity"; Flags: runhidden; RunOnceId: "RemoveApoloPort"; Check: IsAdminInstallMode

[CustomMessages]
brazilianportuguese.TaskFirewall=Liberar o controle remoto no Firewall do Windows
english.TaskFirewall=Allow the remote control through the Windows Firewall
brazilianportuguese.TaskGroupRemote=Controle remoto:
english.TaskGroupRemote=Remote control:
brazilianportuguese.ShortcutStreamDeck=Plugin do Stream Deck
english.ShortcutStreamDeck=Stream Deck plugin
brazilianportuguese.ShortcutFoundry=Módulo do Foundry VTT
english.ShortcutFoundry=Foundry VTT module
brazilianportuguese.FileTypeProject=Projeto do Apolo
english.FileTypeProject=Apolo project
brazilianportuguese.FileTypePacked=Projeto empacotado do Apolo
english.FileTypePacked=Apolo packed project
brazilianportuguese.WelcomeCredits=O Apolo é um player de áudio para mesas de RPG, criado por Igor Teuri (Dados Críticos). Ele é uma modernização do Ares, de Jörg Ruedenauer, a quem pertence o crédito pela base do programa. Software livre, sob a licença GNU GPL (versão 2 ou posterior).
english.WelcomeCredits=Apolo is an audio player for tabletop RPG sessions, created by Igor Teuri (Dados Críticos). It is a modernization of Ares by Jörg Ruedenauer, who deserves the credit for the foundation of the program. Free software under the GNU GPL (version 2 or later).

[Code]
// O Apolo vem com o .NET 8 junto (nao precisa instalar nada alem do Windows 10 ou 11).
procedure InitializeWizard();
begin
  WizardForm.WelcomeLabel2.Caption := CustomMessage('WelcomeCredits');
end;
