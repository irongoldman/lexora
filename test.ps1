param([switch]$Interactive, [switch]$Offline, [switch]$StartupOnly, [switch]$ReplaceOnly)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'test-output'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments = @('/nologo', '/target:exe', '/main:Tests', ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')), ('/out:' + (Join-Path $output 'Tests.exe')),
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll', '/reference:System.Net.Http.dll', '/reference:System.Web.Extensions.dll',
    (Join-Path $PSScriptRoot 'App.cs'), (Join-Path $PSScriptRoot 'StartupSettings.cs'), (Join-Path $PSScriptRoot 'DocumentTarget.cs'), (Join-Path $PSScriptRoot 'Tests.cs'))
foreach ($reference in @('UIAutomationClient.dll', 'UIAutomationTypes.dll', 'WindowsBase.dll')) { $arguments += '/reference:' + (Join-Path (Split-Path $compiler) ('WPF\' + $reference)) }
$arguments += '/win32icon:' + (Join-Path $PSScriptRoot 'assets\Lexora.ico')
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Error al compilar las pruebas.' }
$testArgs = @($output)
if ($Interactive) { $testArgs += '--interactive' }
if ($Offline) { $testArgs += '--offline' }
if ($StartupOnly) { $testArgs += '--startup' }
if ($ReplaceOnly) { $testArgs += '--replace' }
& (Join-Path $output 'Tests.exe') @testArgs
if ($LASTEXITCODE -ne 0) { throw 'Han fallado las pruebas.' }
