$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK is required: https://dotnet.microsoft.com/download/dotnet/8.0'
}
dotnet publish "$root\RedTrace.Windows.csproj" -c Release -r win-x64 --self-contained true -p:Platform=x64 -o "$root\publish"
if ($LASTEXITCODE -ne 0) { throw "RedTrace build failed with exit code $LASTEXITCODE" }
Write-Host "Built $root\publish\RedTrace.exe"
