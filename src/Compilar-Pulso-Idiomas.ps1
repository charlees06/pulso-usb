param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference = 'Stop'
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkPath 'csc.exe'
$wpfPath = Join-Path $frameworkPath 'WPF'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Se necesita .NET Framework 4.8 para compilar Pulso.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = Join-Path (Resolve-Path -LiteralPath $OutputDirectory).Path 'Pulso-Idiomas.exe'
$compileArgs = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+',
    '/r:System.Core.dll', '/r:System.ServiceProcess.dll', '/r:System.Xaml.dll',
    ('/r:' + (Join-Path $wpfPath 'WindowsBase.dll')),
    ('/r:' + (Join-Path $wpfPath 'PresentationCore.dll')),
    ('/r:' + (Join-Path $wpfPath 'PresentationFramework.dll')),
    ('/resource:' + (Join-Path $PSScriptRoot 'PulsoIdiomas.xaml') + ',PulsoV2.xaml'),
    ('/resource:' + (Join-Path $PSScriptRoot 'Idiomas.txt') + ',Idiomas.txt'),
    ('/resource:' + (Join-Path $PSScriptRoot 'Pulso.ico') + ',Pulso.ico'),
    ('/win32icon:' + (Join-Path $PSScriptRoot 'Pulso.ico')),
    ('/out:' + $outputPath),
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'Pulso.manifest')),
    ('/resource:' + (Join-Path $PSScriptRoot 'DriverBundle\HIDUSBF_AS.INF') + ',HIDUSBF_AS.INF'),
    ('/resource:' + (Join-Path $PSScriptRoot 'DriverBundle\amd64_as\hidusbf.sys') + ',HIDUSBF_NOPATCH.SYS'),
    (Join-Path $PSScriptRoot 'AssemblyInfo.cs'),
    (Join-Path $PSScriptRoot 'TaskbarIntegration.cs'),
    (Join-Path $PSScriptRoot 'UsbRateCore.cs'),
    (Join-Path $PSScriptRoot 'PulsoIdiomas.cs'),
    (Join-Path $PSScriptRoot 'Localization.cs'),
    (Join-Path $PSScriptRoot 'LocalizationTests.cs'),
    (Join-Path $PSScriptRoot 'ControllerInput.cs'),
    (Join-Path $PSScriptRoot 'ControllerView.cs'),
    (Join-Path $PSScriptRoot 'ControllerTests.cs'),
    (Join-Path $PSScriptRoot 'DriverPackage.cs')
)
& $compilerPath @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'La compilación no se completó.' }
Write-Output $outputPath
