#ifndef PackageDir
  #define PackageDir "..\dist\Pulso-Completo"
#endif
#ifndef OutputPath
  #define OutputPath "..\dist"
#endif

[Setup]
AppId={{5170B5C2-4450-47A1-A825-A3E5B96C42C7}
AppName=Pulso
AppVersion=2.4.0
AppVerName=Pulso 2.4
AppPublisher=Pulso
DefaultDirName={localappdata}\Programs\Pulso
DefaultGroupName=Pulso
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
WizardStyle=modern dark polar
WizardSizePercent=110
DisableWelcomePage=no
SetupIconFile=..\src\Pulso.ico
UninstallDisplayIcon={app}\Pulso.exe
OutputDir={#OutputPath}
OutputBaseFilename=Pulso-Setup-2.4.0-x64
Compression=lzma2/normal
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
VersionInfoVersion=2.4.0.0
VersionInfoProductName=Pulso
VersionInfoDescription=Pulso USB Control - Setup

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "chinese"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Messages]
spanish.WelcomeLabel1=Tu control USB empieza aquí
spanish.WelcomeLabel2=Instala Pulso para configurar tus dispositivos y probar tu mando.%n%nIncluye los componentes necesarios y cinco idiomas. Tus ajustes USB se guardan solo cuando tú los aplicas.
spanish.FinishedLabel=Pulso ya está instalado.%n%nPara añadirlo a la barra de tareas, abre Pulso y entra en Guía rápida > Anclar a la barra de tareas. Windows puede pedirte confirmación.
english.FinishedLabel=Pulso is installed.%n%nTo add it to your taskbar, open Pulso and go to Quick guide > Pin to taskbar. Windows may ask for confirmation.
portuguese.FinishedLabel=O Pulso está instalado.%n%nPara adicioná-lo à barra de tarefas, abra o Pulso e vá para Guia rápido > Fixar na barra de tarefas. O Windows pode pedir confirmação.
chinese.FinishedLabel=Pulso 已安装。%n%n要将其固定到任务栏，请打开 Pulso 并进入“快速指南”>“固定到任务栏”。Windows 可能会要求确认。
ukrainian.FinishedLabel=Pulso встановлено.%n%nЩоб додати його на панель завдань, відкрийте Pulso та виберіть Посібник > Закріпити на панелі завдань. Windows може попросити підтвердження.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

; Explicit file list: never package local device backups or test output.
[Files]
Source: "{#PackageDir}\Pulso.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Pulso.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Aplicacion\Pulso.exe"; DestDir: "{app}\Aplicacion"; Flags: ignoreversion
Source: "{#PackageDir}\Aplicacion\Pulso.exe.config"; DestDir: "{app}\Aplicacion"; Flags: ignoreversion
Source: "{#PackageDir}\Requisitos\NDP48-x86-x64-AllOS-ENU.exe"; DestDir: "{app}\Requisitos"; Flags: ignoreversion
Source: "{#PackageDir}\LEEME.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\TERCEROS.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\SHA256.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Pulso\Pulso"; Filename: "{app}\Pulso.exe"; WorkingDir: "{app}"; AppUserModelID: "Pulso.UsbControl"
Name: "{autodesktop}\Pulso"; Filename: "{app}\Pulso.exe"; WorkingDir: "{app}"; AppUserModelID: "Pulso.UsbControl"; Tasks: desktopicon

[Run]
Filename: "{app}\Pulso.exe"; Description: "{cm:LaunchProgram,Pulso}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

; Generated backups and shared drivers are intentionally not uninstall targets.
; Pin requests belong to a user action in the foreground app, not to Setup.

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  PreferenceFolder, PreferenceFile, Code: String;
begin
  if CurStep = ssPostInstall then begin
    PreferenceFolder := ExpandConstant('{localappdata}\Pulso');
    PreferenceFile := PreferenceFolder + '\language.txt';
    if not FileExists(PreferenceFile) then begin
      Code := 'es';
      if ActiveLanguage = 'english' then Code := 'en';
      if ActiveLanguage = 'portuguese' then Code := 'pt';
      if ActiveLanguage = 'chinese' then Code := 'zh';
      if ActiveLanguage = 'ukrainian' then Code := 'uk';
      if ForceDirectories(PreferenceFolder) then SaveStringToFile(PreferenceFile, Code, False);
    end;
  end;
end;
