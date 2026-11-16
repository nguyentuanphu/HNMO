#ifndef SourceDir
  #define SourceDir "..\..\..\..\dist\HNMO-win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\..\..\dist"
#endif

[Setup]
AppId={{8D7E4A65-2A7E-4B1B-9F7F-9A41D9F0B501}
AppName=HNMO
AppVersion=1.0
DefaultDirName={localappdata}\Programs\HNMO
DefaultGroupName=HNMO
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=HNMO-Setup
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\HNMO"; Filename: "{app}\web\HNMO.exe"; WorkingDir: "{app}\web"
Name: "{group}\HNMO Control"; Filename: "http://localhost:8080/control"
Name: "{group}\Uninstall HNMO"; Filename: "{uninstallexe}"
Name: "{autodesktop}\HNMO"; Filename: "{app}\web\HNMO.exe"; WorkingDir: "{app}\web"; Tasks: desktopicon

[Run]
Filename: "{app}\web\HNMO.exe"; WorkingDir: "{app}\web"; Description: "Start HNMO now"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SaveDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    SaveDir := ExpandConstant('{app}\save');
    if DirExists(SaveDir) then
    begin
      if not UninstallSilent then
      begin
        if MsgBox('Delete your save data and database in "' + SaveDir + '"? Click No to keep your progress.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        begin
          DelTree(SaveDir, True, True, True);
        end;
      end;
    end;
  end;
end;
