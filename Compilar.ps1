param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'build\app'))
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'src\Compilar-Pulso-Idiomas.ps1') -OutputDirectory $OutputDirectory
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\Pulso.config') -Destination (Join-Path $OutputDirectory 'Pulso-Idiomas.exe.config')
