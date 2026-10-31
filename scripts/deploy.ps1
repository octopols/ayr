# Copies the built app to a portable folder and adds a Start menu shortcut to the tray app.
# No installer, no services: deleting the folder and the shortcut removes Ayr (turn autostart off first if you enabled it).
[CmdletBinding()]
param(
    [string]$Target = 'D:\Portable\Ayr',
    [string]$Shortcut = (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Ayr.lnk')
)
$ErrorActionPreference = 'Stop'
$bin = Join-Path $PSScriptRoot '..\bin'

New-Item -ItemType Directory -Force $Target | Out-Null
foreach ($file in 'Ayr.exe', 'ayrctl.exe') {
    $destination = Join-Path $Target $file
    $aside = "$destination.old"
    Remove-Item $aside -ErrorAction SilentlyContinue
    try {
        Copy-Item (Join-Path $bin $file) $destination -Force
    } catch [System.IO.IOException] {
        # A running exe (the tray or its supervisor) cannot be overwritten but can be renamed; it keeps running from the
        # renamed file and the next start uses the new one.
        Move-Item $destination $aside -Force
        Copy-Item (Join-Path $bin $file) $destination -Force
        Write-Host "$file was in use; previous copy moved to $(Split-Path $aside -Leaf)"
    }
}
# A loaded driver's file can't be replaced, and the shipped files are identical anyway, so only missing ones are copied.
$vendor = Join-Path $Target 'vendor'
New-Item -ItemType Directory -Force $vendor | Out-Null
Get-ChildItem (Join-Path $bin 'vendor') | Where-Object { -not (Test-Path (Join-Path $vendor $_.Name)) } | Copy-Item -Destination $vendor

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($Shortcut)
$link.TargetPath = Join-Path $Target 'Ayr.exe'
$link.WorkingDirectory = $Target
$link.Description = 'Ayr: fan and performance control for MSI laptops'
$link.Save()

Get-ChildItem $Target | Select-Object Name, Length
"shortcut: $Shortcut -> $($link.TargetPath)"
