#define AppName "预处理仿真平台"
#define AppVersion "1.0.0"
#define AppExeName "PreProcess.Wpf.exe"

[Setup]
AppId={{7E638A0B-35F8-4D3D-9866-2F2EAB84AA12}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=PreProcess
VersionInfoVersion=1.0.0.0
VersionInfoCompany=PreProcess
VersionInfoDescription={#AppName}完整离线安装程序
DefaultDirName={localappdata}\Programs\PreProcess.Wpf
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\output\installer
OutputBaseFilename=PreProcess-Windows-x64-Full-Setup
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes
UsePreviousAppDir=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: unchecked

[Dirs]
Name: "{app}\output"

[Files]
Source: "..\GUI_WPF\PreProcess.Wpf\PreProcess.Wpf\bin\Release\PreProcess.Wpf.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\GUI_WPF\PreProcess.Wpf\PreProcess.Wpf\bin\Release\PreProcess.Wpf.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\coreprogram\*"; DestDir: "{app}\coreprogram"; Excludes: "__pycache__\*,*.pyc,*.pyo,*\runs\*,*\reference_runs\*,*\latest_run.json,*\02-程序\output\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\.python-base313\*"; DestDir: "{app}\.python-base313"; Excludes: "__pycache__\*,*.pyc,*.pyo,include\*,libs\*,Scripts\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\.python-runtime\*"; DestDir: "{app}\.python-runtime"; Excludes: ".gitignore,__pycache__\*,*.pyc,*.pyo,*.pdb,*.lib,*.whl,*\tests\*,*\test\*,Lib\site-packages\tensorflow\include\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "prerequisites\NDP48-x86-x64-AllOS-ENU.exe"; Flags: dontcopy

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动{#AppName}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DotNetRestartRequired: Boolean;

function IsDotNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(
    HKLM64,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
    'Release',
    Release) and (Release >= 528040);
  if not Result then
    Result := RegQueryDWordValue(
      HKLM32,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
      'Release',
      Release) and (Release >= 528040);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if IsDotNet48Installed then
    Exit;

  ExtractTemporaryFile('NDP48-x86-x64-AllOS-ENU.exe');
  if not Exec(
    ExpandConstant('{tmp}\NDP48-x86-x64-AllOS-ENU.exe'),
    '/q /norestart',
    '',
    SW_SHOW,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    Result := '无法启动 .NET Framework 4.8 离线安装程序。';
    Exit;
  end;

  if (ResultCode = 1641) or (ResultCode = 3010) then
  begin
    DotNetRestartRequired := True;
    NeedsRestart := True;
  end
  else if ResultCode <> 0 then
    Result := Format('.NET Framework 4.8 安装失败，返回代码：%d。', [ResultCode]);
end;

function NeedRestart: Boolean;
begin
  Result := DotNetRestartRequired;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppRoot: String;
  BaseRoot: String;
  VenvRoot: String;
  VenvConfig: String;
begin
  if CurStep <> ssPostInstall then
    Exit;

  AppRoot := ExpandConstant('{app}');
  BaseRoot := AppRoot + '\.python-base313';
  VenvRoot := AppRoot + '\.python-runtime';
  VenvConfig :=
    'home = ' + BaseRoot + #13#10 +
    'include-system-site-packages = false' + #13#10 +
    'version = 3.13.5' + #13#10 +
    'executable = ' + BaseRoot + '\python.exe' + #13#10 +
    'command = ' + BaseRoot + '\python.exe -m venv ' + VenvRoot + #13#10;

  if not SaveStringToFile(VenvRoot + '\pyvenv.cfg', VenvConfig, False) then
    MsgBox('无法更新内置 Python 环境配置，请检查安装目录权限。', mbError, MB_OK);

  ForceDirectories(AppRoot + '\output');
end;
