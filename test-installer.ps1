$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'test-output'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$executable = Join-Path $output 'InstallerTests.exe'
& $compiler /nologo /target:exe /main:InstallerTests ('/out:' + $executable) /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'Setup.cs') (Join-Path $PSScriptRoot 'StartupSettings.cs') (Join-Path $PSScriptRoot 'InstallerTests.cs') ('/resource:' + (Join-Path $PSScriptRoot 'LICENSE') + ',LICENSE') ('/resource:' + (Join-Path $PSScriptRoot 'TERMS.txt') + ',TERMS.txt')
if ($LASTEXITCODE -ne 0) { throw 'Error al compilar las pruebas del desinstalador.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Han fallado las pruebas del desinstalador.' }
