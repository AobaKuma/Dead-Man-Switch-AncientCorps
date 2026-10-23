<#
  In-game acceptance run for the corps DEFCON / companies on FFF readiness (RimWorld 1.7).
  Same scheme as DMSE.BVRAcceptance/run.ps1.

  1. Builds DMSAC.Acceptance.dll against 1.7/Assemblies/AncientCorps.dll and FFF's 1.7 Fortified.dll (whatever is there now).
  2. Copies it into 1.7/Assemblies for this run only.
  3. Starts RimWorld with -quicktest (new colony) and -dmsac-acceptance=<OutDir>. The runner plays every test, writes
     dmsac_acceptance_log.txt / dmsac_acceptance_results.tsv into OutDir, deletes its temporary save and quits.
  4. Removes the DLL again (also when the game crashes or times out) and copies Player.log into OutDir.

  Usage:  powershell -ExecutionPolicy Bypass -File _source/DMSAC.Acceptance/run.ps1 -OutDir <dir> [-TimeoutMinutes 20] [-Only R4,R9]
  RimWorld must not be running (it locks the assemblies). The active mod list is used as is.
#>
param(
    [Parameter(Mandatory = $true)][string]$OutDir,
    [int]$TimeoutMinutes = 20,
    [string]$GameExe = '',
    [string]$Only = '',
    [string]$SaveDataFolder = ''
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$modRoot = (Resolve-Path (Join-Path $here '..\..')).Path
if ($GameExe -eq '') { $GameExe = (Resolve-Path (Join-Path $modRoot '..\..\RimWorld.exe')).Path }
$assemblies = Join-Path $modRoot '1.7\Assemblies'
$runnerDll = Join-Path $assemblies 'DMSAC.Acceptance.dll'

if (Get-Process -Name 'RimWorld', 'RimWorldWin64' -ErrorAction SilentlyContinue) { throw 'RimWorld is running; close it first.' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Remove-Item (Join-Path $OutDir 'dmsac_acceptance_*') -Force -ErrorAction SilentlyContinue

dotnet build (Join-Path $here 'DMSAC.Acceptance.csproj') -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'acceptance build failed' }

Copy-Item (Join-Path $here 'bin\DMSAC.Acceptance.dll') $runnerDll -Force
$started = Get-Date

try {
    $gameArgs = @('-quicktest', "-dmsac-acceptance=$OutDir")
    if ($Only -ne '') { $gameArgs += "-dmsac-only=$Only" }
    if ($SaveDataFolder -ne '') { $gameArgs += "-savedatafolder=$SaveDataFolder" }
    $proc = Start-Process -FilePath $GameExe -ArgumentList $gameArgs -WorkingDirectory (Split-Path $GameExe) -PassThru
    Write-Output "RimWorld started (pid $($proc.Id)); waiting up to $TimeoutMinutes min..."
    $deadline = $started.AddMinutes($TimeoutMinutes)
    Start-Sleep -Seconds 10
    while ((Get-Process -Name 'RimWorld', 'RimWorldWin64' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 5
    }
    if (Get-Process -Name 'RimWorld', 'RimWorldWin64' -ErrorAction SilentlyContinue) {
        Write-Output 'Timeout: stopping RimWorld.'
        Get-Process -Name 'RimWorld', 'RimWorldWin64' -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Seconds 3
    }
}
finally {
    for ($i = 0; $i -lt 20 -and (Test-Path $runnerDll); $i++) {
        try { Remove-Item $runnerDll -Force } catch { Start-Sleep -Seconds 1 }
    }
    if (Test-Path $runnerDll) { Write-Warning "Could not remove $runnerDll - delete it manually." }
}

$playerLog = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Logs\Player.log'
if ($SaveDataFolder -ne '' -and (Test-Path (Join-Path $SaveDataFolder 'Logs\Player.log'))) { $playerLog = Join-Path $SaveDataFolder 'Logs\Player.log' }
if (Test-Path $playerLog) {
    $log = Get-Item $playerLog
    if ($log.LastWriteTime -ge $started) { Copy-Item $playerLog (Join-Path $OutDir 'Player.log') -Force }
}
$results = Join-Path $OutDir 'dmsac_acceptance_results.tsv'
if (Test-Path $results) { Get-Content $results } else { Write-Output 'No results file: the run did not finish (see Player.log / dmsac_acceptance_log.txt).' }
