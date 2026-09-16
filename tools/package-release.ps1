[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputRoot = '',
    [switch]$SkipBuild,
    [string]$PythonRuntimeRoot = '',
    [switch]$SelfExtract,
    [string]$SevenZip = '',
    [string]$SfxModule = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$OutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) { Join-Path $repoRoot 'artifacts' } else { $OutputRoot }
$project = Join-Path $repoRoot 'GUI_WPF\PreProcess.Wpf\PreProcess.Wpf\PreProcess.Wpf.csproj'
$coreRoot = Join-Path $repoRoot 'coreprogram'
$configFile = Join-Path $coreRoot 'config.json'

if (-not (Test-Path $project)) { throw "WPF project not found: $project" }
if (-not (Test-Path $coreRoot)) { throw "Runtime directory not found: $coreRoot" }

$version = 'dev'
if (Test-Path $configFile) {
    try {
        $metadata = Get-Content $configFile -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($property in @('version', 'package_version', 'release_version')) {
            if ($metadata.$property) { $version = [string]$metadata.$property; break }
        }
    } catch { Write-Warning "Could not parse $configFile; using version '$version'." }
}
$safeVersion = ($version -replace '[^0-9A-Za-z._-]', '_')
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$packageName = "PreProcess-Wpf-$safeVersion-$stamp"
$staging = Join-Path $OutputRoot "$packageName\PreProcess"
$zipPath = Join-Path $OutputRoot "$packageName.zip"

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$workRoot = Join-Path $OutputRoot "$packageName.work"
if (Test-Path $workRoot) { Remove-Item $workRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $staging | Out-Null
New-Item -ItemType Directory -Force -Path $workRoot | Out-Null

if (-not $SkipBuild) {
    $msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if (-not $msbuild) { $msbuild = Get-Command msbuild -ErrorAction SilentlyContinue }
    $msbuildPath = if ($msbuild) { $msbuild.Source } else { '' }
    if (-not $msbuild) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (Test-Path $vswhere) {
            $msbuildPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        }
    }
    if (-not $msbuildPath) {
        throw 'MSBuild was not found. Install Visual Studio Build Tools with .NET Framework 4.8 targeting pack, or use -SkipBuild with an existing Release output.'
    }
    & $msbuildPath $project /t:Rebuild /p:Configuration=$Configuration /p:Platform=AnyCPU /m
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }
}

$buildOutput = Join-Path (Split-Path $project) "bin\$Configuration"
if (-not (Test-Path (Join-Path $buildOutput 'PreProcess.Wpf.exe'))) {
    throw "Build output not found: $buildOutput\PreProcess.Wpf.exe"
}
Copy-Item (Join-Path $buildOutput '*') $staging -Recurse -Force
Copy-Item $coreRoot (Join-Path $staging 'coreprogram') -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'setup-python-runtime.ps1') $staging -Force
Copy-Item (Join-Path $PSScriptRoot 'setup-python-runtime.bat') $staging -Force
if ($PythonRuntimeRoot) {
    $runtimeSource = (Resolve-Path $PythonRuntimeRoot).Path
    if (-not (Test-Path (Join-Path $runtimeSource 'python.exe'))) { throw "PythonRuntimeRoot must contain python.exe: $runtimeSource" }
    $runtimeDestination = Join-Path $staging '.python-runtime'
    # TensorFlow contains paths that exceed Copy-Item's legacy path handling.
    # Robocopy reliably copies the full runtime tree and returns 0-7 for success.
    $robocopy = Join-Path $env:WINDIR 'System32\robocopy.exe'
    if (-not (Test-Path $robocopy)) { throw 'robocopy.exe is required to package the Python runtime.' }
    & $robocopy $runtimeSource $runtimeDestination /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -gt 7) { throw "Failed to copy Python runtime; robocopy exit code: $LASTEXITCODE" }
    $runtimePython = Join-Path $staging '.python-runtime\python.exe'
    & $runtimePython -c "import struct,sys; assert sys.version_info[:2] == (3,10); assert struct.calcsize('P') * 8 == 64; import joblib,keras,matplotlib,numpy,pandas,sklearn,tensorflow; print(sys.version); print('offline dependencies OK')"
    if ($LASTEXITCODE -ne 0) { throw 'The supplied Python runtime is missing required offline dependencies.' }
}

# Runtime output and Python caches are machine-local and are intentionally excluded.
Get-ChildItem (Join-Path $staging 'coreprogram') -Directory -Recurse |
    Where-Object { $_.Name -in @('runs', '__pycache__', 'reference_runs') } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force
Get-ChildItem (Join-Path $staging 'coreprogram') -File -Recurse -Filter 'latest_run.json' |
    Remove-Item -Force
$solverScratchOutputs = Get-ChildItem (Join-Path $staging 'coreprogram') -Directory -Recurse -Filter 'output' |
    Where-Object { Test-Path (Join-Path $_.Parent.FullName 'production_main_output_interface.exe') }
$solverScratchOutputs | Remove-Item -Recurse -Force

@(
    'PreProcess WPF portable package'
    "Version: $version"
    "Build time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
    ''
    'Requirements: Windows x64, .NET Framework 4.8, Python 3.10 x64.'
    ''
    'First use:'
    '1. Confirm that python --version prints Python 3.10.x.'
    '2. Run setup-python-runtime.bat (internet access is required).'
    '3. Run PreProcess.Wpf.exe after setup succeeds.'
    ''
    'If Python 3.10 is not on PATH, run:'
    'powershell -ExecutionPolicy Bypass -File setup-python-runtime.ps1 -PythonExecutable "C:\Python310\python.exe"'
    ''
    'Keep PreProcess.Wpf.exe, PreProcess.Wpf.exe.config, and coreprogram in their original relative locations.'
) | Set-Content (Join-Path $staging 'README.txt') -Encoding UTF8

$manifest = Get-ChildItem $staging -File -Recurse | Where-Object { $_.FullName -notlike "*$([IO.Path]::DirectorySeparatorChar).python-runtime$([IO.Path]::DirectorySeparatorChar)*" } | ForEach-Object {
    [PSCustomObject]@{ path = $_.FullName.Substring($staging.Length + 1); sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash; bytes = $_.Length }
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $staging 'MANIFEST.sha256.json') -Encoding UTF8

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
$tar = Get-Command tar.exe -ErrorAction SilentlyContinue
if ($tar) {
    Push-Location $staging
    try { & $tar.Source -a -c -f $zipPath *; if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" } }
    finally { Pop-Location }
} else {
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -CompressionLevel Optimal
}
$hash = Get-FileHash $zipPath -Algorithm SHA256
$hash | Format-List | Out-File (Join-Path $OutputRoot "$packageName.zip.sha256.txt") -Encoding ASCII
Write-Host "Created: $zipPath"
Write-Host "SHA256:  $($hash.Hash)"

if ($SelfExtract) {
    $iexpress = Join-Path $env:WINDIR 'System32\iexpress.exe'
    if (-not (Test-Path $iexpress)) { throw 'IExpress is required for -SelfExtract.' }
    $sed = Join-Path $workRoot 'package.sed'
    $extractScript = Join-Path $workRoot 'install.ps1'
    $extractCmd = Join-Path $workRoot 'install.cmd'
    @'
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$zip = Join-Path $here 'payload.zip'
$target = Join-Path $here 'PreProcess'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    foreach ($entry in $archive.Entries) {
        $destination = Join-Path $target $entry.FullName
        if ($entry.FullName.EndsWith('/')) { New-Item -ItemType Directory -Force -Path $destination | Out-Null; continue }
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        $input = $entry.Open(); $output = New-Object IO.FileStream($destination, [IO.FileMode]::Create)
        try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
    }
} finally { $archive.Dispose() }
Write-Host "PreProcess installed to $target"
Write-Host "Run $target\PreProcess.Wpf.exe"
'@ | Set-Content $extractScript -Encoding UTF8
    @'
@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
'@ | Set-Content $extractCmd -Encoding ASCII
    Copy-Item $zipPath (Join-Path $workRoot 'payload.zip') -Force
    $sedLines = @(
        '[Version]', 'Class=IEXPRESS', 'SEDVersion=3',
        '[Options]', 'PackagePurpose=InstallApp', 'ShowInstallProgramWindow=0', 'HideExtractAnimation=0', 'UseLongFileName=1', 'InsideCompressed=0', 'CAB_FixedSize=0', 'CAB_ResvCodeSigning=0', 'RebootMode=N', 'InstallPrompt=%InstallPrompt%', 'DisplayLicense=%DisplayLicense%', 'FinishMessage=%FinishMessage%', 'TargetName=%TargetName%', 'FriendlyName=%FriendlyName%', 'AppLaunched=%AppLaunched%', 'PostInstallCmd=%PostInstallCmd%', 'AdminQuietInstCmd=%AdminQuietInstCmd%', 'UserQuietInstCmd=%UserQuietInstCmd%', 'SourceFiles=SourceFiles',
        '[SourceFiles]', ('SourceFiles0=' + $workRoot + '\'), '[SourceFiles0]', '%FILE0%=', '%FILE1%=', '%FILE2%=',
        '[Strings]', 'InstallPrompt=', 'DisplayLicense=', 'FinishMessage=PreProcess has been extracted.', ('TargetName=' + (Join-Path $OutputRoot "$packageName.exe")), 'FriendlyName=PreProcess WPF Installer', 'AppLaunched=install.cmd', 'PostInstallCmd=<None>', 'AdminQuietInstCmd=', 'UserQuietInstCmd=', 'FILE0="payload.zip"', 'FILE1="install.ps1"', 'FILE2="install.cmd"'
    )
    $sedLines | Set-Content $sed -Encoding ASCII
    & $iexpress /N /Q $sed
    $selfExtractPath = Join-Path $OutputRoot ($packageName + '.exe')
    if (-not (Test-Path $selfExtractPath)) {
        Write-Warning "IExpress did not create $selfExtractPath. Run the generated SED manually on a Windows build host."
    } else { Write-Host "Created: $selfExtractPath" }
}

if ($SfxModule) {
    $seven = if ($SevenZip) { $SevenZip } else { (Get-Command 7z.exe,7za.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source) }
    if (-not $seven -or -not (Test-Path $seven)) { throw '7z.exe/7za.exe is required for SFX packaging.' }
    if (-not (Test-Path $SfxModule)) { throw "7zS.sfx not found: $SfxModule" }
    $sfxWork = Join-Path $OutputRoot "$packageName.sfxwork"
    New-Item -ItemType Directory -Force -Path $sfxWork | Out-Null
    $payload7z = Join-Path $sfxWork 'payload.7z'
    Push-Location $staging
    try { & $seven a -t7z -mx=5 $payload7z *; if ($LASTEXITCODE -ne 0) { throw "7z failed: $LASTEXITCODE" } }
    finally { Pop-Location }
    $sfxConfig = Join-Path $sfxWork 'config.txt'
    @(';!@Install@!UTF-8!', 'RunProgram="install.cmd"', 'ExecuteFile="install.cmd"', 'Directory="%TEMP%\\PreProcess"', ';!@InstallEnd@!') |
        Set-Content $sfxConfig -Encoding UTF8
    $sfxExe = Join-Path $OutputRoot "$packageName.exe"
    $out = [IO.File]::Open($sfxExe, [IO.FileMode]::Create)
    try {
        foreach ($file in @($SfxModule, $sfxConfig, $payload7z)) {
            $bytes = [IO.File]::ReadAllBytes($file); $out.Write($bytes, 0, $bytes.Length)
        }
    } finally { $out.Dispose() }
    Write-Host "Created SFX: $sfxExe"
}
