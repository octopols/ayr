# End-to-end test of ayrctl against real hardware. Must run elevated. Ends on Boost and Turbo with the default GPU offsets.
param(
    [string]$Log = (Join-Path $PSScriptRoot '..\bin\e2e-result.txt'),
    [string]$Exe = (Join-Path $PSScriptRoot '..\bin\ayrctl.exe')
)
$ErrorActionPreference = 'Continue'
$script:fail = 0
Remove-Item $Log -ErrorAction SilentlyContinue

function Out-Log([string]$m) { "$(Get-Date -Format HH:mm:ss) $m" | Tee-Object -FilePath $Log -Append }
function Ayrctl([string[]]$a) {
    $o = & $exe @a 2>&1 | Out-String
    Out-Log "> ayrctl $($a -join ' ') (exit $LASTEXITCODE)" | Out-Null
    $o.TrimEnd() -split "`r?`n" | ForEach-Object { Out-Log "    $_" | Out-Null }
    return $o
}
function Check($ok, [string]$what) { if ([bool]$ok) { Out-Log "PASS $what" } else { $script:fail++; Out-Log "FAIL $what" } }
function Supervisors { @(Get-CimInstance Win32_Process -Filter "Name='ayrctl.exe'" | Where-Object { $_.CommandLine -match ' supervise' }) }
function Wait-Until([scriptblock]$cond, [int]$seconds) { for ($i = 0; $i -lt $seconds * 2; $i++) { if (& $cond) { return $true }; Start-Sleep -Milliseconds 500 }; return $false }

Out-Log "elevated: $(([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrators'))"
Ayrctl status | Out-Null

# Fan modes
$o = Ayrctl fan,auto;   Check ($o -match 'Fan mode\s+: auto')   'fan auto'
$o = Ayrctl fan,max;    Check ($o -match 'Fan mode\s+: max' -and $o -match 'CPU 100,100,100,100,100,100,100') 'fan max (curve 100)'
$o = Ayrctl fan,boost;  Check ($o -match 'Fan mode\s+: boost' -and $o -match 'cooler boost on') 'fan boost'
$o = Ayrctl fan,silent; Check ($o -match 'Fan mode\s+: silent') 'fan silent'
Check ((Wait-Until { (Supervisors).Count -eq 1 } 5)) 'supervisor running while Silent'

# Crash safety: kill the supervisor outright; the sentinel must put fans on Auto.
$sup = Supervisors | Select-Object -First 1
if ($sup) {
    Stop-Process -Id $sup.ProcessId -Force
    Out-Log "killed supervisor pid $($sup.ProcessId)"
    Check (Wait-Until { (& $exe status | Out-String) -match 'Fan mode\s+: auto' } 10) 'sentinel restored Auto after supervisor was killed'
}

# Thermal guard: lower the trip point below the current temperature for this test only.
$env:AYR_SILENT_LIMIT = '40'
$o = Ayrctl fan,silent; Check ($o -match 'Fan mode\s+: silent') 'fan silent (limit 40C for test)'
Check (Wait-Until { (& $exe status | Out-String) -match 'Fan mode\s+: auto' } 15) 'silent guard tripped to Auto'
Remove-Item Env:AYR_SILENT_LIMIT
Check (Wait-Until { (Supervisors).Count -eq 0 } 10) 'supervisor exits when nothing needs guarding'
Get-Content (Join-Path (Split-Path $exe) 'data\events.log') -Tail 6 | ForEach-Object { Out-Log "    events: $_" }

# Performance levels
$o = Ayrctl perf,high;   Check ($o -match 'Performance\s+: high'   -and $o -match 'core \+?0 MHz, memory \+?0 MHz') 'perf high, gpu 0/0'
$o = Ayrctl perf,medium; Check ($o -match 'Performance\s+: medium' -and $o -match 'core \+?0 MHz') 'perf medium'
$o = Ayrctl perf,low;    Check ($o -match 'Performance\s+: low'    -and $o -match 'core \+?0 MHz') 'perf low'
$o = Ayrctl perf,turbo;  Check ($o -match 'Performance\s+: turbo'  -and $o -match 'core \+180 MHz, memory \+500 MHz') 'perf turbo, gpu +180/+500'
Check ((Wait-Until { (Supervisors).Count -eq 1 } 5)) 'supervisor running for Turbo'

# Driver-reset simulation: wipe offsets behind the tool's back; the supervisor must reapply them.
$o = Ayrctl gpu,0,0; Check ($o -match 'core \+?0 MHz') 'offsets wiped to 0/0'
Check (Wait-Until { (& $exe status | Out-String) -match 'core \+180 MHz, memory \+500 MHz' } 20) 'supervisor reapplied +180/+500'

# Configurable Turbo offsets (values inside Dragon Center's 0-180 / 0-500 range).
$o = Ayrctl oc,150,400;   Check ($o -match 'applied core \+150 MHz, memory \+400 MHz' -and $o -match 'GPU offsets\s+: core \+150 MHz, memory \+400 MHz') 'oc 150 400 applied while Turbo'
$o = Ayrctl gpu,0,0;      Check ($o -match 'core \+?0 MHz') 'offsets wiped to 0/0 again'
Check (Wait-Until { (& $exe status | Out-String) -match 'GPU offsets\s+: core \+150 MHz, memory \+400 MHz' } 20) 'supervisor reapplied the configured +150/+400, not the default'
$o = Ayrctl perf,high;    Check ($o -match 'GPU offsets\s+: core \+?0 MHz') 'perf high drops offsets'
$o = Ayrctl oc,120,300;   Check ($o -match 'Saved core \+120 MHz, memory \+300 MHz' -and $o -match 'GPU offsets\s+: core \+?0 MHz') 'oc while High is saved, not applied'
$o = Ayrctl perf,turbo;   Check ($o -match 'GPU offsets\s+: core \+120 MHz, memory \+300 MHz') 'perf turbo applies saved +120/+300'
$o = Ayrctl oc,999,1;     Check ($o -match 'must be 0..180') 'out-of-range offset rejected'

# Leave the laptop on Boost and Turbo with the default GPU offsets.
$o = Ayrctl perf,turbo,180,500; Check ($o -match 'GPU offsets\s+: core \+180 MHz, memory \+500 MHz' -and $o -match 'Turbo OC\s+: core \+180 MHz, memory \+500 MHz') 'perf turbo 180 500'
$o = Ayrctl fan,boost
$o = Ayrctl status
Check ($o -match 'Fan mode\s+: boost' -and $o -match 'Performance\s+: turbo' -and $o -match 'core \+180 MHz, memory \+500 MHz') 'final state = boost + turbo + 180/500'
Out-Log "DONE failures=$script:fail"
exit $script:fail
