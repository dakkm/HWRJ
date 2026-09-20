[CmdletBinding()]
param(
    [string]$InstallRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$PythonExecutable = 'python.exe'
)
$ErrorActionPreference = 'Stop'
$InstallRoot = (Resolve-Path $InstallRoot).Path
$requirements = Join-Path $InstallRoot 'coreprogram\requirements-runtime.txt'
$venv = Join-Path $InstallRoot '.python-runtime'
if (-not (Test-Path $requirements)) { throw "Missing requirements file: $requirements" }
$python = Get-Command $PythonExecutable -ErrorAction SilentlyContinue
if (-not $python) { throw "Python 3.13 x64 was not found. Install it and add it to PATH, or pass -PythonExecutable with its full path." }
& $python.Source -c "import struct,sys; assert sys.version_info[:2] == (3,13), 'Python 3.13 required'; assert struct.calcsize('P') * 8 == 64, '64-bit Python required'; print(sys.version)"
if ($LASTEXITCODE -ne 0) { throw 'Python version check failed.' }
if (-not (Test-Path (Join-Path $venv 'Scripts\python.exe'))) {
    & $python.Source -m venv $venv
    if ($LASTEXITCODE -ne 0) { throw 'Failed to create virtual environment.' }
}
$venvPython = Join-Path $venv 'Scripts\python.exe'
& $venvPython -m pip install --upgrade pip
& $venvPython -m pip install -r $requirements
if ($LASTEXITCODE -ne 0) { throw 'Python dependency installation failed.' }
& $venvPython -c "import joblib,keras,matplotlib,numpy,pandas,sklearn,tensorflow; print('Runtime dependencies verified.')"
if ($LASTEXITCODE -ne 0) { throw 'Python dependency verification failed.' }
$configPath = Join-Path $InstallRoot 'PreProcess.Wpf.exe.config'
if (Test-Path $configPath) {
    [xml]$config = Get-Content $configPath -Raw
    $node = $config.configuration.appSettings.add | Where-Object { $_.key -eq 'PythonExecutable' }
    if ($node) { $node.value = '.python-runtime\Scripts\python.exe'; $config.Save($configPath) }
}
Write-Host "Python runtime ready: $venvPython"
