; First install of the unpackaged build. Only that: updates are staged by the app and applied by
; Telegram.Updater, which swaps app.new\ in for app\ and never runs this installer.
;
;   iscc /DAppVersion=13.0.0.0 /DArch=x64 /DPublishDir=<publish folder> /DOutputDir=<folder> Telegram.iss
;
; Build.WinUI.ps1 -Unpackaged runs it.

#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef Arch
  #error Arch (x64 or arm64) is required
#endif
#ifndef PublishDir
  #error PublishDir is required
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

#define AppName "Telegram for Windows (Unigram)"
; Toast.RegisterApplication's, which the app sets on its process: the shortcuts must carry the same
; one, or a pinned shortcut and the running window are two taskbar buttons.
#define AppUserModelId "TelegramFZ-LLC.Windows"

[Setup]
; Never change it: Telegram.Updater writes the version into this entry's {AppId}_is1 key.
AppId={{A3996223-6B78-4784-9615-A466F009E4C6}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName}
AppPublisher=Telegram FZ-LLC
VersionInfoVersion={#AppVersion}

; Fixed, because AppPaths keeps LocalState, LocalCache and TempState under the same root.
DefaultDirName={localappdata}\TelegramFZ-LLC.Windows
PrivilegesRequired=lowest

; There is nothing to ask: the folder is fixed, the shortcuts are always made and the app picks its
; own language. So no page is shown, and the wizard is cut down to a progress bar - see [Code].
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
ShowLanguageDialog=no

; Outside app\, which every update replaces whole.
UninstallFilesDir={app}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\app\Telegram.exe

#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

CloseApplications=force
RestartApplications=no

OutputDir={#OutputDir}
OutputBaseFilename=Telegram-{#AppVersion}-{#Arch}-setup
SetupIconFile=Telegram.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardResizable=no

[InstallDelete]
; A reinstall replaces the version whole, as an update does, instead of leaving files behind.
Type: filesandordirs; Name: "{app}\app"
Type: filesandordirs; Name: "{app}\app.new"
Type: filesandordirs; Name: "{app}\app.old"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}\app"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\app\Telegram.exe"; AppUserModelID: "{#AppUserModelId}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\app\Telegram.exe"; AppUserModelID: "{#AppUserModelId}"

[Registry]
; The app fills it on every start; here it is only created empty, so that uninstall removes it.
Root: HKCU; Subkey: "Software\Classes\AppUserModelId\{#AppUserModelId}"; Flags: uninsdeletekey
; ToastActivator.Clsid, the class the shell starts the app through when a toast is clicked.
Root: HKCU; Subkey: "Software\Classes\CLSID\{{3542F50C-8B9A-4A2B-A47D-9E5EAF762103}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\app\Telegram.exe"; Flags: nowait skipifsilent

[UninstallDelete]
; The whole root, data included, as removing a package removes its LocalState. Also the only way
; files an update added get removed: the uninstall log knows only the first install's.
Type: filesandordirs; Name: "{app}"

[Code]
// The wizard reduced to the status line and the progress bar, both moved out of the page notebook
// onto the form itself: everything else the form has is hidden, and the install still updates
// the two controls wherever they are parented.
procedure InitializeWizard();
var
  Width, Height: Integer;
begin
  Width := WizardForm.Width;
  Height := WizardForm.Height;

  WizardForm.OuterNotebook.Visible := False;
  WizardForm.Bevel.Visible := False;

  WizardForm.ClientWidth := ScaleX(440);
  WizardForm.ClientHeight := ScaleY(96);

  WizardForm.StatusLabel.Parent := WizardForm;
  WizardForm.StatusLabel.SetBounds(ScaleX(24), ScaleY(24), ScaleX(392), ScaleY(16));

  WizardForm.ProgressGauge.Parent := WizardForm;
  WizardForm.ProgressGauge.SetBounds(ScaleX(24), ScaleY(52), ScaleX(392), ScaleY(20));

  // Placed for the full size already, so centred again for the one it has now.
  WizardForm.Left := WizardForm.Left + (Width - WizardForm.Width) div 2;
  WizardForm.Top := WizardForm.Top + (Height - WizardForm.Height) div 2;
end;

function SetTimer(Wnd: HWND; IDEvent, Elapse: LongWord; TimerFunc: LongWord): LongWord;
  external 'SetTimer@user32.dll stdcall';
function KillTimer(Wnd: HWND; IDEvent: LongWord): BOOL;
  external 'KillTimer@user32.dll stdcall';

var
  StartTimer: LongWord;

procedure StartInstall(Wnd: HWND; Msg: LongWord; IDEvent: LongWord; Time: LongWord);
begin
  KillTimer(0, StartTimer);
  WizardForm.NextButton.OnClick(WizardForm.NextButton);
end;

// Inno shows the Ready page whenever no page comes before it, so that a setup never starts
// without a click. There is nothing on it to read here, so it is clicked through - from a timer,
// because it is the first page and so arrives before the form is on screen, where a click does
// nothing.
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
  begin
    StartTimer := SetTimer(0, 0, 1, CreateCallback(@StartInstall));
  end
  else if CurPageID = wpInstalling then
  begin
    WizardForm.BackButton.Visible := False;
    WizardForm.NextButton.Visible := False;
    WizardForm.CancelButton.Visible := False;
  end;
end;
