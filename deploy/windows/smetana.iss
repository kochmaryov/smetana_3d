; ============================================================================
; SmetaNA — конфигурация установщика Windows (Inno Setup 6.3+)
;
; Выбор Inno Setup обоснован в docs/release.md (раздел «Установщики»):
; пер-пользовательская установка без администратора (школьные ПК), RU/EN,
; страница EULA, тихий режим для аудиторий (/SILENT), код-сигнинг exe и
; деинсталлятора. Сборка: deploy\windows\build-installer.ps1.
;
; Определяемые параметры (передаются ISCC через /D):
;   AppVersion     — версия продукта, например 1.0.0
;   AppVersionQuad — версия в числовом виде 1.0.0.0 (VersionInfoVersion)
;   PublishDir     — каталог среза deploy\publish.ps1 (artifacts\publish\win-x64)
;   Sign           — если определён, включается подпись (SignTool=smetana)
; ============================================================================

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef AppVersionQuad
  #define AppVersionQuad "0.1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\artifacts\publish\win-x64"
#endif

#define AppName "SmetaNA"
#define AppExeName "SmetaNA.EditorUI.exe"

[Setup]
AppId={{7E2B4C41-6D3A-4E2F-9B6C-5B1A9D2F3E41}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SmetaNA
AppPublisherURL=https://smetana.example.ru
AppSupportURL=https://smetana.example.ru/support
AppUpdatesURL=https://smetana.example.ru/download
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Пер-пользовательская установка: работает без прав администратора (школьные ПК).
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\artifacts\installers
OutputBaseFilename=SmetaNA-{#AppVersion}-win-x64
; Перед релизом добавить иконку: SetupIconFile=assets\smetana.ico (чек-лист в docs/release.md).
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersionQuad}
VersionInfoProductTextVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
; Лицензионное соглашение — на первом шаге установки (черновик RU; EN — в docs/legal).
LicenseFile=..\..\docs\legal\EULA.ru.md
LicenseAcceptedLabel=Я принимаю условия / I accept the terms
LicenseRejectedLabel=Не принимаю / I do not accept

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Полный срез приложения: бинарники, runtimes/<rid>/native (Manifold, lib3mf), docs/legal.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; Подпись exe и деинсталлятора — при сборке с /DSign и /Ssmetana=... (см. build-installer.ps1).
#ifdef Sign
SignedUninstaller=yes
SignTool=smetana
#endif
