$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& "$root\build.ps1"
$destination = Join-Path $env:LOCALAPPDATA 'RedTrace'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Get-Process RedTrace -ErrorAction SilentlyContinue | Stop-Process -Force
Copy-Item "$root\publish\*" $destination -Recurse -Force
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\RedTrace.lnk'))
$shortcut.TargetPath = Join-Path $destination 'RedTrace.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.Save()
Start-Process (Join-Path $destination 'RedTrace.exe')
Write-Host "RedTrace installed in $destination"
