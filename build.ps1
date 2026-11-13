param(
    [ValidateSet('App', 'NativeTest', 'BrowserTest')]
    [string]$Target = 'App',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$gac = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL'
$references = @('System.Windows.Forms.dll', 'System.Drawing.dll')
$references += 'UIAutomationClient', 'UIAutomationTypes', 'WindowsBase' | ForEach-Object {
    Get-ChildItem -LiteralPath (Join-Path $gac $_) -Recurse -Filter "$_.dll" |
        Select-Object -First 1 -ExpandProperty FullName
}

$directory = New-Item -ItemType Directory -Force -Path $OutputDirectory
$arguments = @('/nologo', '/codepage:65001', '/optimize+', '/platform:x64')
$arguments += $references | ForEach-Object { "/reference:$_" }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName)
if ($Target -eq 'App') {
    $arguments += '/target:winexe', "/out:$($directory.FullName)\NumberWheel.exe"
} else {
    $file = if ($Target -eq 'NativeTest') { 'Probe.cs' } else { 'BrowserProbe.cs' }
    $sources += Join-Path $PSScriptRoot "tests\$file"
    $arguments += '/target:exe', '/main:Probe', "/out:$($directory.FullName)\$Target.exe"
}
& (Join-Path $framework 'csc.exe') @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Number Wheel compilation failed.' }
