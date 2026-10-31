# Watches the GPU while you play or run a GPU load with Turbo (+180 core / +500 memory) active.
# Reports driver resets (nvlddmkm errors, Display event 4101), offsets dropping, and peak clocks/temps.
# Read-only: changes nothing.
[CmdletBinding()]
param(
    [int]$Minutes = 10,
    [string]$Ayrctl = 'D:\Portable\Ayr\ayrctl.exe',
    [string]$OutFile = (Join-Path $env:TEMP ("ayr-stability-{0}.csv" -f (Get-Date -Format 'yyyyMMdd-HHmmss')))
)
$ErrorActionPreference = 'Continue'
$start = Get-Date
$end = $start.AddMinutes($Minutes)
$fields = 'temperature.gpu,utilization.gpu,clocks.sm,clocks.mem,pstate,power.draw'
"time,gpu_temp,gpu_util,sm_mhz,mem_mhz,pstate,power_w,core_offset,mem_offset" | Set-Content $OutFile

$offsetLost = 0; $samples = 0; $loaded = 0
$peakSm = 0; $peakMem = 0; $peakTemp = 0
$core = ''; $mem = ''
while ((Get-Date) -lt $end) {
    if ($samples % 15 -eq 0) {
        $status = & $Ayrctl status | Out-String
        if ($status -match 'core ([+-]?\d+) MHz, memory ([+-]?\d+) MHz') { $core = $Matches[1]; $mem = $Matches[2] }
        if ($core -ne '+180' -or $mem -ne '+500') { $offsetLost++ }
    }
    $line = (& nvidia-smi "--query-gpu=$fields" --format=csv,noheader,nounits 2>$null | Select-Object -First 1)
    if ($line) {
        $p = $line -split '\s*,\s*'
        $temp = [int]$p[0]; $util = [int]$p[1]; $sm = [int]$p[2]; $memClk = [int]$p[3]
        if ($util -ge 50) { $loaded++ }
        $peakSm = [Math]::Max($peakSm, $sm); $peakMem = [Math]::Max($peakMem, $memClk); $peakTemp = [Math]::Max($peakTemp, $temp)
        "$(Get-Date -Format HH:mm:ss),$($p -join ','),$core,$mem" | Add-Content $OutFile
    }
    $samples++
    Start-Sleep -Seconds 2
}

$resets = Get-WinEvent -FilterHashtable @{ LogName = 'System'; StartTime = $start } -ErrorAction SilentlyContinue |
    Where-Object { $_.ProviderName -match 'nvlddmkm' -or ($_.ProviderName -eq 'Display' -and $_.Id -eq 4101) }

""
"Stability check $($start.ToString('HH:mm'))-$((Get-Date).ToString('HH:mm')), $samples samples, $loaded under load (>=50% util)"
"Peak: SM $peakSm MHz, memory $peakMem MHz, GPU $peakTemp C"
"Offset checks not at +180/+500: $offsetLost"
"Driver reset / nvlddmkm events: $(@($resets).Count)"
$resets | Select-Object TimeCreated, ProviderName, Id, @{ n = 'Message'; e = { ($_.Message -split "`n")[0] } } | Format-Table -AutoSize | Out-String -Width 200
"CSV: $OutFile"
if (@($resets).Count -eq 0 -and $offsetLost -eq 0 -and $loaded -gt 0) { "RESULT: PASS" }
elseif ($loaded -eq 0) { "RESULT: INCONCLUSIVE - the GPU was never under load" }
else { "RESULT: FAIL - see above" }
