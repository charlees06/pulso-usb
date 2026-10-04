param([string]$CompilerPath, [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
if (-not $CompilerPath) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $candidates = @()
    if ($compiler) { $candidates += $compiler.Source }
    $candidates += @((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),(Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'))
    $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) { throw 'Instala Inno Setup 6.7.3 o indica -CompilerPath con la ruta de ISCC.exe.' }
& (Join-Path $PSScriptRoot 'Probar.ps1')
& (Join-Path $PSScriptRoot 'src\Empaquetar-Pulso.ps1') -OutputDirectory $OutputDirectory
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
$package = Join-Path $output 'Pulso-Completo'
& $CompilerPath ('/DPackageDir='+$package) ('/DOutputPath='+$output) (Join-Path $PSScriptRoot 'installer\Pulso.iss')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el instalador.' }
$assets = @('Pulso-Setup-2.4.0-x64.exe','Pulso-Completo-Windows-x64.zip')
$checksums = foreach ($name in $assets) { (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $name }
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'),$checksums,[Text.UTF8Encoding]::new($false))
Write-Output (Join-Path $output 'Pulso-Setup-2.4.0-x64.exe')
