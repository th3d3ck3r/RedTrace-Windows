$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK is required: https://dotnet.microsoft.com/download/dotnet/8.0'
}
dotnet publish "$root\RedTrace.Windows.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$root\publish"
Write-Host "Built $root\publish\RedTrace.exe"
