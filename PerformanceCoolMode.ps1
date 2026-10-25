<#
PerformanceCoolMode.ps1

HASEE/Clevo CV15S tuning script for i7-12650H + RTX 4060 Laptop.

Usage:
  powershell -ExecutionPolicy Bypass -File .\PerformanceCoolMode.ps1 game
  powershell -ExecutionPolicy Bypass -File .\PerformanceCoolMode.ps1 work
  powershell -ExecutionPolicy Bypass -File .\PerformanceCoolMode.ps1 max
  powershell -ExecutionPolicy Bypass -File .\PerformanceCoolMode.ps1 status

Profiles:
  game: better FPS than LibraryMode, lower heat than full performance.
  work: higher CPU ceiling for compiling/rendering, still cooler than full turbo.
  max: restore aggressive CPU behavior for plugged-in full performance.

Note:
  This machine/driver reports NVIDIA power limit control as N/A in WDDM mode,
  so GPU fan/power cannot be reliably locked by nvidia-smi. The script reduces
  heat mainly by controlling CPU boost and Windows power policy.
#>

#Requires -RunAsAdministrator

$GUID = @{
    SCHEME_BALANCED      = "381b4222-f694-41f0-9685-ff5bb260df2e"
    SUB_PROCESSOR        = "54533251-82be-4824-96c1-47b60b740d00"
    SUB_PCIEXPRESS       = "501a4d13-42af-4429-9fd1-a8218c268e20"
    SUB_WIRELESS         = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1"
    PROCTHROTTLEMIN      = "893dee8e-2bef-41e0-89c6-b55d0929964c"
    PROCTHROTTLEMAX      = "bc5038f7-23e0-4960-96da-33abaf5935ec"
    PROCFREQMAX          = "75b0ae3f-bce0-45a7-8c89-c9611c25e100"
    PROCFREQMAX1         = "75b0ae3f-bce0-45a7-8c89-c9611c25e101"
    PERFBOOSTMODE        = "be337238-0d82-4146-a960-4f3749d470c7"
    PERFBOOSTPOL         = "45bcc044-d885-43e2-8605-ee0ec6e96b59"
    PERFEPP              = "36687f9e-e3a5-4dbf-b1dc-15eb381c6863"
    PERFEPP1             = "36687f9e-e3a5-4dbf-b1dc-15eb381c6864"
    SYSCOOLPOL           = "94d3a615-a899-4ac5-ae2b-e4d8f634367f"
    PERFINCTHRESHOLD     = "06cadf0e-64ed-448a-8927-ce7bf90eb35d"
    PERFDECTHRESHOLD     = "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6"
    LATENCYHINTPERF      = "619b7505-003b-4e82-b7a6-4dd29c300971"
    HETEROCLASS0FLOOR    = "fddc842b-8364-4edc-94cf-c17f60de1c80"
    ASPM                 = "ee12f906-d277-404b-b6da-e5fa1a576df5"
    WIRELESS_POWER       = "12bbebe6-58d6-4636-95bb-3217ef867c1a"
}

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Set-PowerSetting($subGuid, $settingGuid, $value, $scheme = $GUID.SCHEME_BALANCED) {
    powercfg /setacvalueindex $scheme $subGuid $settingGuid $value 2>&1 | Out-Null
    powercfg /setdcvalueindex $scheme $subGuid $settingGuid $value 2>&1 | Out-Null
}

function Apply-PowerScheme {
    powercfg /setactive $GUID.SCHEME_BALANCED 2>&1 | Out-Null
}

function Set-CommonPerformanceBase {
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCTHROTTLEMIN "5"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCTHROTTLEMAX "100"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.SYSCOOLPOL "1"
    Set-PowerSetting $GUID.SUB_PCIEXPRESS $GUID.ASPM "0"
    Set-PowerSetting $GUID.SUB_WIRELESS $GUID.WIRELESS_POWER "0"
}

function Enable-GameCoolMode {
    Write-Host ""
    Write-Host "[GameCool] Applying cooler gaming profile..." -ForegroundColor Cyan
    Set-CommonPerformanceBase
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCFREQMAX "3600"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCFREQMAX1 "3200"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTMODE "6"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTPOL "40"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP "20"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP1 "30"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFINCTHRESHOLD "30"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFDECTHRESHOLD "10"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.LATENCYHINTPERF "70"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.HETEROCLASS0FLOOR "60"
    Apply-PowerScheme
    Write-Host "[GameCool] CPU boost enabled, capped at 3.6GHz P-core / 3.2GHz E-core." -ForegroundColor Green
    Write-Host "[GameCool] Use this for most games: higher FPS than LibraryMode, much cooler than max turbo." -ForegroundColor Green
    Show-Status
}

function Enable-WorkCoolMode {
    Write-Host ""
    Write-Host "[WorkCool] Applying cooler productivity profile..." -ForegroundColor Cyan
    Set-CommonPerformanceBase
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCFREQMAX "4000"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCFREQMAX1 "3500"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTMODE "4"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTPOL "55"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP "15"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP1 "25"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFINCTHRESHOLD "20"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFDECTHRESHOLD "8"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.LATENCYHINTPERF "85"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.HETEROCLASS0FLOOR "70"
    Apply-PowerScheme
    Write-Host "[WorkCool] CPU boost enabled, capped at 4.0GHz P-core / 3.5GHz E-core." -ForegroundColor Green
    Write-Host "[WorkCool] Use this for compile, IDE, light rendering, and multitasking." -ForegroundColor Green
    Show-Status
}

function Enable-MaxMode {
    Write-Host ""
    Write-Host "[Max] Restoring aggressive performance profile..." -ForegroundColor Magenta
    Set-CommonPerformanceBase
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCFREQMAX "0"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCFREQMAX1 "0"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTMODE "2"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTPOL "60"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP "10"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP1 "15"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFINCTHRESHOLD "20"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFDECTHRESHOLD "10"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.LATENCYHINTPERF "99"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.HETEROCLASS0FLOOR "100"
    Apply-PowerScheme
    Write-Host "[Max] Full turbo behavior restored. Temperatures will be higher." -ForegroundColor Yellow
    Show-Status
}

function Get-Temperature {
    $thermalZone = Get-WmiObject -Namespace "root/wmi" -Class MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue
    if ($thermalZone) {
        return [math]::Round(($thermalZone.CurrentTemperature / 10 - 273.15), 1)
    }
    return $null
}

function Get-CPUInfo {
    $cpu = Get-CimInstance Win32_Processor
    return @{ CurrentGHz = [math]::Round($cpu.CurrentClockSpeed / 1000, 2); MaxGHz = [math]::Round($cpu.MaxClockSpeed / 1000, 2) }
}

function Get-GPUInfo {
    $gpu = nvidia-smi --query-gpu=temperature.gpu,power.draw,utilization.gpu,pstate,clocks.current.graphics,clocks.current.memory --format=csv,noheader 2>$null
    if ($gpu) {
        $parts = $gpu -split ', ' | ForEach-Object { $_.Trim() }
        return @{ Temp = $parts[0]; Power = $parts[1]; Util = $parts[2]; PState = $parts[3]; CoreClock = $parts[4]; MemClock = $parts[5] }
    }
    return $null
}

function Show-Status {
    Write-Host ""
    Write-Host "-- Current status --" -ForegroundColor Yellow
    $temp = Get-Temperature
    if ($temp) { Write-Host "CPU temp: $temp C" }
    $cpu = Get-CPUInfo
    if ($cpu) { Write-Host "CPU clock: $($cpu.CurrentGHz) GHz (reported max $($cpu.MaxGHz) GHz)" }
    $gpu = Get-GPUInfo
    if ($gpu) {
        Write-Host "GPU temp: $($gpu.Temp) C"
        Write-Host "GPU power/util: $($gpu.Power), $($gpu.Util), $($gpu.PState)"
        Write-Host "GPU clocks: core $($gpu.CoreClock), memory $($gpu.MemClock)"
    }
    Write-Host "Tip: Set ControlCenter to Performance mode, and set fan mode to Auto or a custom curve capped near 45-55% for gaming." -ForegroundColor Gray
}

if (-not (Test-Admin)) {
    Write-Host "Run PowerShell as Administrator." -ForegroundColor Red
    exit 1
}

$action = $args[0]
if (-not $action) { $action = "status" }

switch ($action.ToLower()) {
    "game" { Enable-GameCoolMode }
    "work" { Enable-WorkCoolMode }
    "max" { Enable-MaxMode }
    "status" { Show-Status }
    default {
        Write-Host "Usage: .\PerformanceCoolMode.ps1 { game | work | max | status }" -ForegroundColor Yellow
        Show-Status
    }
}
