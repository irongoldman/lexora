param()
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'No está disponible el compilador de .NET Framework de 64 bits.' }
$outputDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$buildDir = Join-Path ([IO.Path]::GetTempPath()) ('Lexora-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildDir | Out-Null
$application = Join-Path $buildDir 'Lexora.exe'
$setup = Join-Path $buildDir 'Instalar-Lexora.exe'
$sourceArchive = Join-Path $buildDir 'SourceCode.zip'
try {
    $sourceFiles = @('App.cs', 'DocumentTarget.cs', 'Setup.cs', 'StartupSettings.cs', 'app.manifest', 'build.ps1', 'test.ps1', 'test-installer.ps1', 'Tests.cs', 'InstallerTests.cs', 'README.md', 'INSTALL.md', 'VALIDACION.md', 'LICENSE', 'TERMS.txt', '.gitignore', 'assets') | ForEach-Object { Join-Path $projectRoot $_ }
    Compress-Archive -LiteralPath $sourceFiles -DestinationPath $sourceArchive
    $framework = Split-Path -Parent $compiler
    $iconPath = Join-Path $projectRoot 'assets\Lexora.ico'
    if (-not (Test-Path -LiteralPath $iconPath)) { & (Join-Path $projectRoot 'assets\Build-Icon.ps1') }
    $references = @('System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Net.Http.dll', 'System.Web.Extensions.dll')
    $mainArguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')), ('/out:' + $application), ('/win32icon:' + $iconPath))
    foreach ($reference in $references) { $mainArguments += '/reference:' + (Join-Path $framework $reference) }
    foreach ($reference in @('UIAutomationClient.dll', 'UIAutomationTypes.dll', 'WindowsBase.dll')) { $mainArguments += '/reference:' + (Join-Path $framework ('WPF\' + $reference)) }
    $mainArguments += Join-Path $projectRoot 'App.cs'
    $mainArguments += Join-Path $projectRoot 'StartupSettings.cs'
    $mainArguments += Join-Path $projectRoot 'DocumentTarget.cs'
    & $compiler @mainArguments
    if ($LASTEXITCODE -ne 0) { throw 'Ha fallado la compilación de la aplicación.' }
    $setupArguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', ('/out:' + $setup), '/reference:System.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', ('/resource:' + $application + ',SeleccionTraductor.exe'), (Join-Path $projectRoot 'Setup.cs'), (Join-Path $projectRoot 'StartupSettings.cs'), ('/win32icon:' + $iconPath), ('/resource:' + $iconPath + ',Lexora.ico'))
    foreach ($file in @('LICENSE', 'TERMS.txt')) { $setupArguments += '/resource:' + (Join-Path $projectRoot $file) + ',' + $file }
    $setupArguments += '/resource:' + $sourceArchive + ',SourceCode.zip'
    & $compiler @setupArguments
    if ($LASTEXITCODE -ne 0) { throw 'Ha fallado la compilación del instalador.' }
    Copy-Item -LiteralPath $setup -Destination (Join-Path $outputDir 'Instalar-Lexora.exe') -Force
    foreach ($oldName in @('Lexora.exe', 'Desinstalar-Lexora.exe', 'SeleccionTraductor.exe', 'Instalar-SeleccionTraductor.exe')) {
        $oldFile = Join-Path $outputDir $oldName
        if (Test-Path -LiteralPath $oldFile -PathType Leaf) { Remove-Item -LiteralPath $oldFile -Force }
    }
    Write-Host "Instalador único: $(Join-Path $outputDir 'Instalar-Lexora.exe')"
} finally {
    foreach ($temporary in @($application, $setup, $sourceArchive)) {
        if (Test-Path -LiteralPath $temporary -PathType Leaf) { Remove-Item -LiteralPath $temporary -Force }
    }
    [IO.Directory]::Delete($buildDir, $false)
}

