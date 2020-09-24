; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "openBVE"
#define MyAppVersion "1.7.1.8"
#define MyAppPublisher "The OpenBVE Project"
#define MyAppURL "http://www.openbve-project.net"
#define OpenBVEExecutable "OpenBve.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{D617A45D-C2F6-44D1-A85C-CA7FFA91F7FC}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableDirPage=auto
DisableProgramGroupPage=auto
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardSmallImageFile=installer_logo.bmp

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "catalan"; MessagesFile: "compiler:Languages\Catalan.isl"
Name: "corsican"; MessagesFile: "compiler:Languages\Corsican.isl"
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "finnish"; MessagesFile: "compiler:Languages\Finnish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "greek"; MessagesFile: "compiler:Languages\Greek.isl"
Name: "hebrew"; MessagesFile: "compiler:Languages\Hebrew.isl"
Name: "hungarian"; MessagesFile: "compiler:Languages\Hungarian.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "norwegian"; MessagesFile: "compiler:Languages\Norwegian.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "scottishgaelic"; MessagesFile: "compiler:Languages\ScottishGaelic.isl"
Name: "serbiancyrillic"; MessagesFile: "compiler:Languages\SerbianCyrillic.isl"
Name: "serbianlatin"; MessagesFile: "compiler:Languages\SerbianLatin.isl"
Name: "slovenian"; MessagesFile: "compiler:Languages\Slovenian.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkablealone
Name: "desktopicon2"; Description: "Create a desktop shortcut to the openBVE Addons folder"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkablealone

[Files]
;Open BVE Main Folder.
Source: "..\..\bin_release\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
;Custom Config File
Source: "InstallerData\filesystem_appdata.cfg"; DestDir: "{app}\";
Source: "InstallerData\filesystem_programfolder.cfg"; DestDir: "{app}\";
;MS .NET 4.6.1 Web Installer.
Source: "InstallerData\NDP461-KB3102438-Web.exe"; DestDir: "{app}"; Flags: deleteafterinstall; AfterInstall: AfterMyProgInstall('AllFilesCopy')
[Icons]
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#OpenBVEExecutable}"; Tasks: desktopicon
Name: "{userdesktop}\openBVE Addons"; Filename:"{code:GetDataDir}"; Tasks: desktopicon2
Name: "{group}\{#MyAppName}"; Filename: "{app}\OpenBVE.exe"
Name: "{group}\Developer Tools\Route Viewer"; Filename: "{app}\RouteViewer.exe"
Name: "{group}\Developer Tools\Object Viewer"; Filename: "{app}\ObjectViewer.exe"
Name: "{group}\Developer Tools\Train Editor"; Filename: "{app}\TrainEditor2.exe"
Name: "{group}\openBVE Addons"; Filename: "{code:GetDataDir}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{app}\openBVE Addons"; Filename: "{code:GetDataDir}"; Flags: uninsneveruninstall

[Dirs]
Name: {code:GetDataDir}; Flags: uninsneveruninstall

[Run]
 Filename: "{app}\{#OpenBVEExecutable}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: postinstall shellexec skipifsilent runascurrentuser
 Filename: "{code:GetDataDir}"; Description: "Open the openBVE Addons Folder" ; Flags: postinstall shellexec waituntilterminated skipifsilent unchecked

[Code]
var
  UsagePage: TInputOptionWizardPage;
  DataDirPage: TInputDirWizardPage;
  use_Net4: Cardinal;
  ResultCode_Net4: Integer;
  
procedure InitializeWizard;
begin
  { Create the pages }

  UsagePage := CreateInputOptionPage(wpSelectTasks,
    'Select Destination Location', 'Where should openBVE Addons be installed?',
    'Please specify the installation location for openBVE Addons.'#13#10 +
    'NOTE: This may be changed at a later date using the Options dialog.',
    True, False);

  UsagePage.Add('Default  - Use AppData\Roaming\openBVE\UserData');
  UsagePage.Add('Program  - Use the UserData folder in the openBVE installation directory');
  UsagePage.Add('Custom   - Select a custom folder');

  DataDirPage := CreateInputDirPage(UsagePage.ID,
    'Select openBVE Addons Directory', 'Where should openBVE Addons be installed?',
    'Please select the folders in which you wish to install openBVE Addons, and then click Next.',
    False, '');

    DataDirPage.Add('Routes:');
    //'Select Destination Location', 'Where should openBVE Addons be installed?'
    DataDirPage.Add('Trains:');
    DataDirPage.Add('Other:');
  { Set default values, using settings that were stored last time if possible }

  case GetPreviousData('UsageMode', '') of
    'Default': UsagePage.SelectedValueIndex := 0;
    'Assembly': UsagePage.SelectedValueIndex := 1;
    'Custom': UsagePage.SelectedValueIndex := 2;
  else
    UsagePage.SelectedValueIndex := 0;

  end;
    DataDirPage.Values[0] := GetPreviousData('DataDir0', ExpandConstant('{userappdata}\{#MyAppName}\LegacyContent\Railway'));
    DataDirPage.Values[1] := GetPreviousData('DataDir1', ExpandConstant('{userappdata}\{#MyAppName}\LegacyContent\Train'));
    DataDirPage.Values[2] := GetPreviousData('DataDir2', ExpandConstant('{userappdata}\{#MyAppName}\LegacyContent\Other'));
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
var
  UsageMode: String;
begin
  { Store the settings so we can restore them next time }
  case UsagePage.SelectedValueIndex of
    0: UsageMode := 'Default';
    1: UsageMode := 'Assembly';
    2: UsageMode := 'Custom';
  end;
  SetPreviousData(PreviousDataKey, 'UsageMode', UsageMode);
  SetPreviousData(PreviousDataKey, 'DataDir0', DataDirPage.Values[0]);
  SetPreviousData(PreviousDataKey, 'DataDir1', DataDirPage.Values[1]);
  SetPreviousData(PreviousDataKey, 'DataDir2', DataDirPage.Values[2]);
end;

function GetDataDir(Param: String): String;
begin
  { Return the selected DataDir }
  Result := DataDirPage.Values[0];
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  { Skip pages that shouldn't be shown }
if (PageID = DataDirPage.ID) and (UsagePage.SelectedValueIndex <> 2) then
    begin
    //If this is a NEW install, and we've selected one of the default route/ train install locations
    //Skip the folder selection page
    Result := True;
    end
else if (PageID = UsagePage.ID) then
    begin
    //Detect whether we've got an existing filsystem.cfg file present
    if (FileExists(ExpandConstant('{userappdata}\{#MyAppName}\Settings\FileSystem.cfg')) or FileExists(ExpandConstant('{app}\UserData\Settings\filesystem.cfg'))) then
      begin
      //Existing install with cfg present, don't show the configuration dialog
      Result := True;
      end
    else
      //No existing filesystem.cfg , so ask the user for the directories
      Result := False;
  end
else
      //Catch-all final false
      Result := False;
end;

function GetHKLM: Integer;
begin
{ Determine whether it is 64bit OS. }
  if IsWin64 then
    Result := HKLM64
  else
    Result := HKLM32;
end;

//Executed after file installation
procedure AfterMyProgInstall(S: String);
var
Installed: Cardinal;
FileLines: TArrayOfString;
  begin
  //Determine whether the component is installed. 
    if RegQueryDWordValue(GetHKLM(),'Software\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release',Installed) then begin
      if Installed >= 394254 then begin
        use_Net4:=1;
      end else begin
        use_Net4:=0;
      end;
    end;
    begin
    if use_Net4 = 1 then begin
    end else begin
    WizardForm.FilenameLabel.Caption := 'Installing Microsoft .NET Framework 4.6.1';
      if Exec(ExpandConstant('{app}\NDP461-KB3102438-Web.exe'), '/norestart /passive /showrmui', '', SW_SHOW,
        ewWaitUntilTerminated, ResultCode_Net4) then 
        begin
        IntToStr(ResultCode_Net4)
        // handle success if necessary; ResultCode contains the exit code
        end
      else begin
        // handle failure if necessary; ResultCode contains the error code;
      end;
    end;
      //OpenBVE filesystem.cfg
      if (UsagePage.SelectedValueIndex = 0) then
      begin
        CreateDir(ExpandConstant('{userappdata}\{#MyAppName}'));
        ForceDirectories(ExpandConstant('{{userappdata}\{#MyAppName}\Settings'));
        FileCopy(ExpandConstant('{app}\filesystem_appdata.cfg'), ExpandConstant('{userappdata}\{#MyAppName}\Settings\filesystem.cfg'), True);
        DeleteFile(ExpandConstant('{app}\filesystem_programfolder.cfg'));
        DataDirPage.Values[0] := ExpandConstant('{userappdata}\{#MyAppName}\LegacyContent\Railway');
        DataDirPage.Values[1] := ExpandConstant('{userappdata}\{#MyAppName}\LegacyContent\Train');
      end;
      if (UsagePage.SelectedValueIndex = 1) then
      begin
        ForceDirectories(ExpandConstant('{app}\UserData\Settings'));
        FileCopy(ExpandConstant('{app}\filesystem_programfolder.cfg'), ExpandConstant('{app}\UserData\Settings\filesystem.cfg'), True);
        DeleteFile(ExpandConstant('{app}\filesystem_appdata.cfg'));
        DataDirPage.Values[0] := ExpandConstant('{app}\UserData');

      end;
      if (UsagePage.SelectedValueIndex = 2) then
      begin
        ForceDirectories(ExpandConstant('{app}\UserData\Settings'));
        //Load filesystem.cfg
        LoadStringsFromFile(ExpandConstant('{app}\UserData\Settings\filesystem.cfg'), FileLines);
        FileLines[2]:='InitialRoute = '+DataDirPage.Values[0];
        FileLines[3]:='InitialTrain = '+DataDirPage.Values[1];
        FileLines[4]:='RoutePackageInstall = '+DataDirPage.Values[0];
        FileLines[5]:='TrainPackageInstall = '+DataDirPage.Values[1];
        //Save filesystem.cfg
        SaveStringsToUTF8File(ExpandConstant('{app}\UserData\Settings\filesystem.cfg'),FileLines,false);
      end;
        ForceDirectories(DataDirPage.Values[0]);
        ForceDirectories(DataDirPage.Values[1])

    end;
end;

function NeedRestart(): Boolean;
begin
  if ResultCode_Net4 = 3010 then
    begin
    Result := True;
    end;
end;
//==========Uninstall Section==========
function InitializeUninstall(): Boolean;
  begin
  MsgBox('Add-on data will not be deleted during uninstallation.', mbError, MB_OK);
  Result := True
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
ResultCode: Integer;
begin
  case CurUninstallStep of usDone:
      begin
        Exec('explorer.exe',ExpandConstant('{app}') , '', SW_SHOW, ewNoWait,ResultCode);
      end;
  end;
end;
