; First install of the unpackaged build. Only that: updates are staged by the app and applied by
; Telegram.Updater, which copies the changed files from app.new\ over app\ and never runs this installer.
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
#define LinkProgId AppUserModelId + ".Link"

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

; Windows 10 1809, the Windows App SDK's floor and Telegram.WinUI.csproj's TargetPlatformMinVersion.
MinVersion=10.0.17763

CloseApplications=force
RestartApplications=no

OutputDir={#OutputDir}
OutputBaseFilename=Telegram-{#AppVersion}-{#Arch}-setup
SetupIconFile=Telegram.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern windows11 dynamic hidebevels includetitlebar
; The light style is off-white unless told otherwise.
WizardBackColor=white
WizardBackColorDynamicDark=black
WizardResizable=no

[InstallDelete]
; A reinstall replaces the version whole instead of leaving files behind, and drops any update staged.
Type: filesandordirs; Name: "{app}\app"
Type: filesandordirs; Name: "{app}\app.new"
Type: filesandordirs; Name: "{app}\app.old"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}\app"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
; The logo under the progress bar, read from {tmp} by [Code].
Source: "Telegram.ico"; Flags: dontcopy

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\app\Telegram.exe"; AppUserModelID: "{#AppUserModelId}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\app\Telegram.exe"; AppUserModelID: "{#AppUserModelId}"

[Registry]
; The app fills it on every start; here it is only created empty, so that uninstall removes it.
Root: HKCU; Subkey: "Software\Classes\AppUserModelId\{#AppUserModelId}"; Flags: uninsdeletekey
; ToastActivator.Clsid, the class the shell starts the app through when a toast is clicked.
Root: HKCU; Subkey: "Software\Classes\CLSID\{{3542F50C-8B9A-4A2B-A47D-9E5EAF762103}"; Flags: uninsdeletekey
; The app's settings, which ApplicationData.GetForUnpackaged keeps here: removed with the data folder.
Root: HKCU; Subkey: "Software\Classes\Local Settings\Software\TelegramFZ-LLC"; Flags: uninsdeletekey
; Launch at startup, which the app writes when it is turned on (UnpackagedStartup), and the choice
; Startup apps records under the same name. Neither is created here.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppUserModelId}"; Flags: uninsdeletevalue dontcreatekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: none; ValueName: "{#AppUserModelId}"; Flags: uninsdeletevalue dontcreatekey

; tg:, tonsite: and tc:, which the packaged flavours declare in their manifest. The command is the
; form Windows App SDK's AppInstance decodes into a protocol activation, as
; RegisterForProtocolActivation would write it.
;
; The scheme keys belong to every app that handles them, so they are only filled in where missing
; and never removed, and their own command is set only where no app has one yet - see
; IsSchemeUnclaimed. The ProgID and capabilities are what list the app in Default apps and in the
; picker when another one already handles the scheme.
Root: HKCU; Subkey: "Software\Classes\{#LinkProgId}"; ValueType: string; ValueName: ""; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\{#LinkProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\app\Telegram.exe"",0"
Root: HKCU; Subkey: "Software\Classes\{#LinkProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\app\Telegram.exe"" ""----ms-protocol:%1"""
Root: HKCU; Subkey: "Software\{#AppUserModelId}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\{#AppUserModelId}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"
Root: HKCU; Subkey: "Software\{#AppUserModelId}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "{#AppName}"
Root: HKCU; Subkey: "Software\{#AppUserModelId}\Capabilities\URLAssociations"; ValueType: string; ValueName: "tg"; ValueData: "{#LinkProgId}"
Root: HKCU; Subkey: "Software\{#AppUserModelId}\Capabilities\URLAssociations"; ValueType: string; ValueName: "tonsite"; ValueData: "{#LinkProgId}"
Root: HKCU; Subkey: "Software\{#AppUserModelId}\Capabilities\URLAssociations"; ValueType: string; ValueName: "tc"; ValueData: "{#LinkProgId}"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#AppName}"; ValueData: "Software\{#AppUserModelId}\Capabilities"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\tg"; ValueType: string; ValueName: ""; ValueData: "URL:tg"; Flags: createvalueifdoesntexist
Root: HKCU; Subkey: "Software\Classes\tg"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: createvalueifdoesntexist
Root: HKCU; Subkey: "Software\Classes\tg\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\app\Telegram.exe"" ""----ms-protocol:%1"""; Check: IsSchemeUnclaimed('tg')
Root: HKCU; Subkey: "Software\Classes\tonsite"; ValueType: string; ValueName: ""; ValueData: "URL:tonsite"; Flags: createvalueifdoesntexist
Root: HKCU; Subkey: "Software\Classes\tonsite"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: createvalueifdoesntexist
Root: HKCU; Subkey: "Software\Classes\tonsite\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\app\Telegram.exe"" ""----ms-protocol:%1"""; Check: IsSchemeUnclaimed('tonsite')
Root: HKCU; Subkey: "Software\Classes\tc"; ValueType: string; ValueName: ""; ValueData: "URL:tc"; Flags: createvalueifdoesntexist
Root: HKCU; Subkey: "Software\Classes\tc"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: createvalueifdoesntexist
Root: HKCU; Subkey: "Software\Classes\tc\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\app\Telegram.exe"" ""----ms-protocol:%1"""; Check: IsSchemeUnclaimed('tc')

[Run]
Filename: "{app}\app\Telegram.exe"; Flags: nowait skipifsilent

[UninstallDelete]
; The whole root, data included, as removing a package removes its LocalState. Also the only way
; files an update added get removed: the uninstall log knows only the first install's.
Type: filesandordirs; Name: "{app}"

[CustomMessages]
Installing=Installing…
Brand=Telegram

[Code]
const
  FormWidth = 560;
  FormHeight = 280;
  BarWidth = 432;
  LogoSize = 24;
  LogoGap = 8;

var
  TitleLabel: TNewStaticText;

// The wizard reduced to a title, the progress bar and the logo. The page notebook is hidden and
// the bar is moved out of it onto the form, where the install still updates it.
procedure InitializeWizard();
var
  Width, Height: Integer;
  Logo: TBitmapImage;
  Brand: TNewStaticText;
  BrandWidth: Integer;
begin
  Width := WizardForm.Width;
  Height := WizardForm.Height;

  WizardForm.Caption := '';
  WizardForm.BorderIcons := [biSystemMenu, biMinimize];
  WizardForm.OuterNotebook.Visible := False;
  WizardForm.Bevel.Visible := False;

  WizardForm.ClientWidth := ScaleX(FormWidth);
  WizardForm.ClientHeight := ScaleY(FormHeight);

  // Out of the client area rather than hidden: Inno shows them again on its own as pages change,
  // and they would land on the bar. Enter and Esc still reach them.
  WizardForm.BackButton.Top := WizardForm.ClientHeight + ScaleY(100);
  WizardForm.NextButton.Top := WizardForm.ClientHeight + ScaleY(100);
  WizardForm.CancelButton.Top := WizardForm.ClientHeight + ScaleY(100);

  TitleLabel := TNewStaticText.Create(WizardForm);
  TitleLabel.Parent := WizardForm;
  TitleLabel.AutoSize := False;
  TitleLabel.WordWrap := True;
  TitleLabel.Alignment := taCenter;
  TitleLabel.Font.Size := 12;
  TitleLabel.Caption := CustomMessage('Installing');
  TitleLabel.SetBounds(ScaleX(32), ScaleY(64), ScaleX(FormWidth - 64), ScaleY(48));

  WizardForm.ProgressGauge.Parent := WizardForm;
  WizardForm.ProgressGauge.SetBounds(ScaleX((FormWidth - BarWidth) div 2), ScaleY(124), ScaleX(BarWidth), ScaleY(10));

  Brand := TNewStaticText.Create(WizardForm);
  Brand.Parent := WizardForm;
  Brand.Font.Size := 12;
  Brand.Caption := CustomMessage('Brand');
  BrandWidth := ScaleX(LogoSize + LogoGap) + Brand.Width;

  Logo := TBitmapImage.Create(WizardForm);
  Logo.Parent := WizardForm;
  Logo.SetBounds((WizardForm.ClientWidth - BrandWidth) div 2, ScaleY(216), ScaleX(LogoSize), ScaleY(LogoSize));
  ExtractTemporaryFile('Telegram.ico');
  InitializeBitmapImageFromIcon(Logo, ExpandConstant('{tmp}\Telegram.ico'), clNone, [16, 20, 24, 32, 40, 48, 64]);

  Brand.Left := Logo.Left + ScaleX(LogoSize + LogoGap);
  Brand.Top := Logo.Top + (Logo.Height - Brand.Height) div 2;

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

procedure ClickNext(Wnd: HWND; Msg: LongWord; IDEvent: LongWord; Time: LongWord);
begin
  KillTimer(0, StartTimer);
  WizardForm.NextButton.OnClick(WizardForm.NextButton);
end;

// Both pages are clicked through from a timer: the first page arrives before the form is on
// screen, where a click does nothing.
procedure CurPageChanged(CurPageID: Integer);
begin
  // Inno shows the Ready page whenever no page comes before it, so that a setup never starts
  // without a click. There is nothing on it to read here.
  if CurPageID = wpReady then
  begin
    StartTimer := SetTimer(0, 0, 1, CreateCallback(@ClickNext));
  end
  else if CurPageID = wpPreparing then
  begin
    // The running app holds the files being replaced, and Inno stops to ask whether to close it.
    // Running the installer already answers that.
    if WizardForm.PreparingYesRadio.Visible then
    begin
      WizardForm.PreparingYesRadio.Checked := True;
      StartTimer := SetTimer(0, 0, 1, CreateCallback(@ClickNext));
    end
    // Raised on every install, also when the page is passed through without being shown: the
    // label then holds a placeholder.
    else if WizardForm.PreparingErrorBitmapImage.Visible then
    begin
      // An error the install cannot go past, which only this page would have shown.
      TitleLabel.Caption := WizardForm.PreparingLabel.Caption;
    end;
  end;
end;

// Through HKCR, so that a handler registered for the whole machine counts as well: the per-user
// command would take precedence over it.
function IsSchemeUnclaimed(Scheme: String): Boolean;
var
  Command: String;
begin
  Result := not RegQueryStringValue(HKCR, Scheme + '\shell\open\command', '', Command)
    or (Pos(Lowercase(ExpandConstant('{app}\app\Telegram.exe')), Lowercase(Command)) > 0);
end;

// Only while it is still this app's: another one may have set its own since.
procedure RemoveSchemeCommand(Scheme: String);
var
  Command: String;
begin
  if RegQueryStringValue(HKCU, 'Software\Classes\' + Scheme + '\shell\open\command', '', Command)
    and (Pos(Lowercase(ExpandConstant('{app}\app\Telegram.exe')), Lowercase(Command)) > 0) then
  begin
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\' + Scheme + '\shell');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveSchemeCommand('tg');
    RemoveSchemeCommand('tonsite');
    RemoveSchemeCommand('tc');
  end;
end;
