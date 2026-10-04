param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\outputs'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$pulsoRuntimePath = Join-Path $PSScriptRoot 'vendor\NDP48-x86-x64-AllOS-ENU.exe'
if (-not (Test-Path -LiteralPath $pulsoRuntimePath)) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $pulsoRuntimePath) -Force | Out-Null
    Invoke-WebRequest -Uri 'https://download.microsoft.com/download/f/3/a/f3a6af84-da23-40a5-8d1c-49cc10c8e76f/NDP48-x86-x64-AllOS-ENU.exe' -OutFile $pulsoRuntimePath
}
if ((Get-FileHash -LiteralPath $pulsoRuntimePath -Algorithm SHA256).Hash -ne '0A3A390C47E639D0F7FC65B21195FEE6B7F65B066F80F70C60FAB191D14B7E40') { throw 'El instalador de .NET no coincide con la versión verificada.' }
$pulsoDriverPath = Join-Path $PSScriptRoot 'DriverBundle\amd64_as\hidusbf.sys'
foreach ($pulsoSignedFile in @($pulsoRuntimePath,$pulsoDriverPath)) {
    $pulsoSignature = Get-AuthenticodeSignature -LiteralPath $pulsoSignedFile
    if ($pulsoSignature.Status -ne 'Valid' -or $pulsoSignature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw "Firma no válida: $pulsoSignedFile" }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$pulsoDestination = (Resolve-Path -LiteralPath $OutputDirectory).Path
$pulsoPackage = Join-Path $pulsoDestination 'Pulso-Completo'
$pulsoApp = Join-Path $pulsoPackage 'Aplicacion'
$pulsoRequirements = Join-Path $pulsoPackage 'Requisitos'
New-Item -ItemType Directory -Path $pulsoApp,$pulsoRequirements -Force | Out-Null
$pulsoBuild = Join-Path $PSScriptRoot 'package-build'
& (Join-Path $PSScriptRoot 'Compilar-Pulso-Idiomas.ps1') -OutputDirectory $pulsoBuild
Copy-Item -LiteralPath (Join-Path $pulsoBuild 'Pulso-Idiomas.exe') -Destination (Join-Path $pulsoApp 'Pulso.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Pulso.config') -Destination (Join-Path $pulsoApp 'Pulso.exe.config')
Copy-Item -LiteralPath $pulsoRuntimePath -Destination (Join-Path $pulsoRequirements 'NDP48-x86-x64-AllOS-ENU.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LEEME-Paquete.txt') -Destination (Join-Path $pulsoPackage 'LEEME.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TERCEROS.txt') -Destination (Join-Path $pulsoPackage 'TERCEROS.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Launcher.config') -Destination (Join-Path $pulsoPackage 'Pulso.exe.config')
$pulsoCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$pulsoLaunchArgs = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/r:System.Windows.Forms.dll',('/win32manifest:'+(Join-Path $PSScriptRoot 'Pulso.manifest')),('/win32icon:'+(Join-Path $PSScriptRoot 'Pulso.ico')),('/out:'+(Join-Path $pulsoPackage 'Pulso.exe')),(Join-Path $PSScriptRoot 'PackageLauncher.cs'))
$pulsoLaunchArgs += (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
& $pulsoCompiler @pulsoLaunchArgs
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el lanzador.' }
# Explicit file allowlist: device backups and local test output never enter the ZIP.
$pulsoFiles = @('Pulso.exe','Pulso.exe.config','LEEME.txt','TERCEROS.txt','Aplicacion\Pulso.exe','Aplicacion\Pulso.exe.config','Requisitos\NDP48-x86-x64-AllOS-ENU.exe')
$pulsoManifest = foreach ($pulsoRelative in $pulsoFiles) { [pscustomobject]@{ file=$pulsoRelative.Replace('\','/'); sha256=(Get-FileHash -LiteralPath (Join-Path $pulsoPackage $pulsoRelative) -Algorithm SHA256).Hash } }
[IO.File]::WriteAllText((Join-Path $pulsoPackage 'SHA256.json'),($pulsoManifest | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
$pulsoFiles += 'SHA256.json'
$pulsoZipPath = Join-Path $pulsoDestination 'Pulso-Completo-Windows-x64.zip'
if (Test-Path -LiteralPath $pulsoZipPath) { Remove-Item -LiteralPath $pulsoZipPath }
$pulsoZip = [IO.Compression.ZipFile]::Open($pulsoZipPath,[IO.Compression.ZipArchiveMode]::Create)
try { foreach ($pulsoRelative in $pulsoFiles) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($pulsoZip,(Join-Path $pulsoPackage $pulsoRelative),$pulsoRelative.Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal) | Out-Null } }
finally { $pulsoZip.Dispose() }
Write-Output $pulsoZipPath
