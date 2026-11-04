# Creates a new project from this template. All logic lives in tools/init/init.cs.
# Usage: ./init.ps1 --help
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'The .NET 10 SDK is required: https://dotnet.microsoft.com/download'
    exit 1
}
& dotnet run tools/init/init.cs -- @args
exit $LASTEXITCODE
