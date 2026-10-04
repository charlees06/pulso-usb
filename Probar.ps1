$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Compilar.ps1')
$report = Join-Path $PSScriptRoot 'build\pruebas.txt'
$process = Start-Process -FilePath (Join-Path $PSScriptRoot 'build\app\Pulso-Idiomas.exe') -ArgumentList @('--self-test',('"'+$report+'"')) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { throw 'Las pruebas siguen pendientes; no se publicará el paquete.' }
$process.Refresh()
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $report; throw 'Las pruebas fallaron.' }
Write-Output 'Pruebas correctas. El informe local está en build/pruebas.txt.'
