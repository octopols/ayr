# Builds Ayr: two x64 .NET Framework 4.8 executables from the same sources.
#   Ayr.exe      windowless tray app (the Start menu entry); with arguments it runs the same commands as ayrctl
#   ayrctl.exe   console command line
#   vendor\      MSI's signed drivers and libraries, copied from the repository's vendor folder
# x64 because MSI's GPU overclock library (GInf.dll) is 64-bit and is loaded in-process.
# .NET Framework because it ships with Windows: nothing to install, and the app stays portable.
[CmdletBinding()]
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\bin'),
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.4.0',
    [string]$InformationalVersion = $Version
)
$ErrorActionPreference = 'Stop'

function Find-Compiler {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) { throw 'vswhere.exe not found: install Visual Studio or the VS Build Tools.' }
    $csc = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
    if (-not $csc) { throw 'The Roslyn C# compiler (csc.exe) was not found in any Visual Studio installation.' }
    return $csc
}

$source = Join-Path $PSScriptRoot '..\src\Ayr'
$assets = Join-Path $source 'Ui\Assets'
$framework = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319'
$objDir = Join-Path $OutDir 'obj'
New-Item -ItemType Directory -Force $OutDir, $objDir | Out-Null

# The version is stamped at build time so a release tag is its single source of truth.
$versionFile = Join-Path $objDir 'AssemblyVersion.g.cs'
@"
[assembly: System.Reflection.AssemblyVersion("$Version.0")]
[assembly: System.Reflection.AssemblyFileVersion("$Version.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("$InformationalVersion")]
"@ | Set-Content $versionFile -Encoding UTF8

$sources = @(Get-ChildItem $source -Recurse -Filter *.cs | ForEach-Object FullName) + $versionFile
$references = 'mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Management.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll',
    'System.ServiceProcess.dll', 'System.Runtime.Serialization.dll', 'System.Xml.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll' |
    ForEach-Object { "/reference:$(Join-Path $framework $_)" }
$resources = 'app.ico', 'tray.ico', 'mark-24.png', 'mark-32.png', 'mark-40.png', 'mark-48.png' |
    ForEach-Object { "/resource:$(Join-Path $assets $_),Ayr.Ui.$_" }
$common = @('/nologo', '/noconfig', '/nostdlib+', '/platform:x64', '/optimize+', '/langversion:7.3', '/warnaserror+') + $references + $resources + @(
    "/win32icon:$(Join-Path $assets 'app.ico')",
    "/win32manifest:$(Join-Path $source 'app.manifest')")

$csc = Find-Compiler
$targets = @(
    @{ Name = 'Ayr.exe'; Arguments = @('/target:winexe', '/define:TRAY_HOST') },
    @{ Name = 'ayrctl.exe'; Arguments = @('/target:exe') }
)
foreach ($target in $targets) {
    $output = Join-Path $OutDir $target.Name
    & $csc @common @($target.Arguments) "/out:$output" @sources
    if ($LASTEXITCODE -ne 0) { throw "csc failed for $($target.Name) with exit code $LASTEXITCODE" }
    Write-Host "built $((Resolve-Path $output).Path)"
}

# MSI's signed files ship next to the executables; Ayr verifies each signature before using it.
$vendorOut = Join-Path $OutDir 'vendor'
New-Item -ItemType Directory -Force $vendorOut | Out-Null
Copy-Item (Join-Path $PSScriptRoot '..\vendor\*') $vendorOut -Force
Write-Host "copied MSI files to $((Resolve-Path $vendorOut).Path)"
