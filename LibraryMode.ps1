<#
╔═══════════════════════════════════════════════════════════════════╗
║               图书馆均衡模式 — Library Mode                      ║
║               神舟 CV15S · i7-12650H · RTX 4060                  ║
╠═══════════════════════════════════════════════════════════════════╣
║  用法:                                                           ║
║    .\LibraryMode.ps1 on      开启图书馆模式 (安静高效)           ║
║    .\LibraryMode.ps1 off     恢复性能模式                         ║
║    .\LibraryMode.ps1 status  查看当前状态                         ║
║    .\LibraryMode.ps1 gui     打开风扇控制面板 (手动调速)          ║
╚═══════════════════════════════════════════════════════════════════╝

原理说明:
  这台机器的 i7-12650H 在睿频时可飙到 115W+，风扇必然起飞。
  本脚本禁用睿频(PERFBOOSTMODE=0)，让 CPU 跑在基础频率 2.3GHz，
  功耗锁定在 ~45W，同时用 nvidia-smi 将 RTX 4060 的功耗墙降到 40W。
  这样系统总发热大幅降低，风扇转速自然维持在低水平。

  CPU 2.3GHz + GPU 40W 对于编程、文献阅读、Office 等场景完全够用。
#>

#Requires -RunAsAdministrator

$script:OriginalVals = @{}

# ---------- GUID 常量 ----------
$GUID = @{
    SCHEME_BALANCED = "381b4222-f694-41f0-9685-ff5bb260df2e"
    SUB_PROCESSOR   = "54533251-82be-4824-96c1-47b60b740d00"
    PERFBOOSTMODE   = "be337238-0d82-4146-a960-4f3749d470c7"
    PROCTHROTTLEMAX = "bc5038f7-23e0-4960-96da-33abaf5935ec"
    PROCTHROTTLEMIN = "893dee8e-2bef-41e0-89c6-b55d0929964c"
    PERFEPP         = "36687f9e-e3a5-4dbf-b1dc-15eb381c6863"
    PERFEPP1        = "36687f9e-e3a5-4dbf-b1dc-15eb381c6864"
    PERFBOOSTPOL    = "45bcc044-d885-43e2-8605-ee0ec6e96b59"
    SYSCOOLPOL      = "94d3a615-a899-4ac5-ae2b-e4d8f634367f"
    PERFINCTHRESHOLD = "06cadf0e-64ed-448a-8927-ce7bf90eb35d"
    PERFDECTHRESHOLD = "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6"
    LATENCYHINTPERF = "619b7505-003b-4e82-b7a6-4dd29c300971"
    HETEROCLASS0FLOOR = "fddc842b-8364-4edc-94cf-c17f60de1c80"
    ASPM            = "ee12f906-d277-404b-b6da-e5fa1a576df5"
}

# ---------- 辅助函数 ----------
function Set-PowerSetting($subGuid, $settingGuid, $value, $scheme=$GUID.SCHEME_BALANCED) {
    powercfg /setacvalueindex $scheme $subGuid $settingGuid $value 2>&1 | Out-Null
    powercfg /setdcvalueindex $scheme $subGuid $settingGuid $value 2>&1 | Out-Null
}

function Apply-PowerScheme {
    powercfg /setactive $GUID.SCHEME_BALANCED 2>&1 | Out-Null
}

function Get-Temperature {
    $tz = Get-WmiObject -Namespace "root/wmi" -Class MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue
    if ($tz) {
        $celsius = [math]::Round(($tz.CurrentTemperature / 10 - 273.15), 1)
        return $celsius
    }
    return $null
}

function Get-CPUInfo {
    $cpu = Get-CimInstance Win32_Processor
    $freq = [math]::Round($cpu.CurrentClockSpeed / 1000, 2)
    $maxFreq = [math]::Round($cpu.MaxClockSpeed / 1000, 2)
    return @{ CurrentGHz = $freq; MaxGHz = $maxFreq }
}

function Get-GPUInfo {
    $gpu = nvidia-smi --query-gpu=temperature.gpu,power.draw,power.limit,fan.speed,utilization.gpu --format=csv,noheader 2>$null
    if ($gpu) {
        $parts = $gpu -split ', ' | ForEach-Object { $_.Trim() }
        return @{
            Temp       = $parts[0]
            PowerDraw  = $parts[1]
            PowerLimit = $parts[2]
            FanSpeed   = $parts[3]
            Util       = $parts[4]
        }
    }
    return $null
}

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# ---------- 核心功能 ----------
function Enable-LibraryMode {
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║   开启图书馆均衡模式...              ║" -ForegroundColor Cyan
    Write-Host "  ╚══════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "  [1/5] 禁用 CPU 睿频 (Turbo Boost)..." -NoNewline
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTMODE "0"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCTHROTTLEMAX "63"
    Write-Host " Done" -ForegroundColor Green

    Write-Host "  [2/5] 调整能效偏好 (EPP 50%, 更偏节能)..." -NoNewline
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP "50"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP1 "50"
    Write-Host " Done" -ForegroundColor Green

    Write-Host "  [3/5] 降低性能增强策略 (更平滑的调频)..." -NoNewline
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTPOL "14"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFINCTHRESHOLD "50"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFDECTHRESHOLD "10"
    Write-Host " Done" -ForegroundColor Green

    Write-Host "  [4/5] 调整延迟与 P-core 底线..." -NoNewline
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.LATENCYHINTPERF "32"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.HETEROCLASS0FLOOR "32"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.SYSCOOLPOL "1"
    Write-Host " Done" -ForegroundColor Green

    Apply-PowerScheme

    Write-Host "  [5/5] 限制 GPU 功耗墙 (40W)..." -NoNewline
    try {
        $null = nvidia-smi -i 0 -pl 40 2>&1
        Write-Host " Done" -ForegroundColor Green
    } catch {
        Write-Host " 跳过 (nvidia-smi不可用)" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "  ✓ 图书馆模式已启用" -ForegroundColor Cyan
    Write-Host "  CPU: 睿频关闭, 基础 2.3GHz, 功耗 ~45W" -ForegroundColor Cyan
    Write-Host "  GPU: 功耗墙 40W (原 115W)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  建议: 可在 ControlCenter30 中把" -ForegroundColor Yellow
    Write-Host "  [风扇] 设为 [安静模式], [场景] 设为 [省电/安静]" -ForegroundColor Yellow

    Show-Status
}

function Disable-LibraryMode {
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "  ║   恢复性能模式...                    ║" -ForegroundColor Magenta
    Write-Host "  ╚══════════════════════════════════════╝" -ForegroundColor Magenta
    Write-Host ""

    Write-Host "  [1/5] 恢复 CPU 睿频..." -NoNewline
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTMODE "2"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PROCTHROTTLEMAX "64"
    Write-Host " Done" -ForegroundColor Green

    Write-Host "  [2/5] 恢复能效偏好..." -NoNewline
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP "21"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFEPP1 "21"
    Write-Host " Done" -ForegroundColor Green

    Write-Host "  [3/5] 恢复性能增强策略..." -NoNewline
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFBOOSTPOL "3c"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFINCTHRESHOLD "3c"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.PERFDECTHRESHOLD "14"
    Write-Host " Done" -ForegroundColor Green

    Write-Host "  [4/5] 恢复延迟与 P-core 设置..." -NoNewline
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.LATENCYHINTPERF "63"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.HETEROCLASS0FLOOR "64"
    Set-PowerSetting $GUID.SUB_PROCESSOR $GUID.SYSCOOLPOL "1"
    Write-Host " Done" -ForegroundColor Green

    Apply-PowerScheme

    Write-Host "  [5/5] 恢复 GPU 功耗..." -NoNewline
    try {
        $null = nvidia-smi -i 0 -pl 115 2>&1
        Write-Host " Done" -ForegroundColor Green
    } catch {
        Write-Host " 跳过" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "  ✓ 已恢复性能模式" -ForegroundColor Magenta
    Write-Host "  CPU: 睿频开启, 最高 ~4.7GHz" -ForegroundColor Magenta
    Write-Host "  GPU: 功耗墙 115W (原厂默认)" -ForegroundColor Magenta

    Show-Status
}

function Show-Status {
    Write-Host ""
    Write-Host "  ── 当前状态 ──" -ForegroundColor Yellow

    # 检查 PERFBOOSTMODE
    $bm = powercfg /q $GUID.SCHEME_BALANCED $GUID.SUB_PROCESSOR $GUID.PERFBOOSTMODE 2>&1
    if ($bm -match "0x([0-9a-f]+)") {
        $bmVal = [int]("0x$($Matches[1])")
        $modeNames = @{0="禁用睿频(图书馆)";1="已启用";2="高性能(默认)";3="高效率";4="高性能高效率"}
        $modeName = if ($modeNames.ContainsKey($bmVal)) { $modeNames[$bmVal] } else { "模式$bmVal" }
        Write-Host "  CPU睿频: $modeName" -ForegroundColor $(if ($bmVal -eq 0) { "Cyan" } else { "Magenta" })
    }

    # CPU 温度
    $temp = Get-Temperature
    if ($temp) {
        $color = if ($temp -gt 85) { "Red" } elseif ($temp -gt 70) { "Yellow" } else { "Green" }
        Write-Host "  CPU温度: $temp°C" -ForegroundColor $color
    }

    # CPU 频率
    $cpu = Get-CPUInfo
    if ($cpu) {
        Write-Host "  CPU频率: $($cpu.CurrentGHz) GHz (上限 $($cpu.MaxGHz) GHz)"
    }

    # GPU 信息
    $gpu = Get-GPUInfo
    if ($gpu) {
        $gpuTemp = $gpu.Temp
        $gpuColor = if ($gpuTemp -gt 80) { "Red" } elseif ($gpuTemp -gt 65) { "Yellow" } else { "Green" }
        Write-Host "  GPU温度: $gpuTemp°C" -ForegroundColor $gpuColor
        Write-Host "  GPU功耗: $($gpu.PowerDraw) / $($gpu.PowerLimit)" -ForegroundColor $(if ($gpu.PowerLimit -match "40") { "Cyan" } else { "Magenta" })
        Write-Host "  GPU使用率: $($gpu.Util)"
        Write-Host "  风扇转速: $($gpu.FanSpeed)"
    }

    Write-Host ""
    Write-Host "  提示: 风扇转速可能无法直接读取 (显示N/A)，" -ForegroundColor Gray
    Write-Host "  运行 .\LibraryMode.ps1 gui 可打开神舟风扇面板手动调速" -ForegroundColor Gray
}

# ---------- 入口 ----------
if (-not (Test-Admin)) {
    Write-Host "  [!] 需要管理员权限，请右键 PowerShell 以管理员身份运行" -ForegroundColor Red
    exit 1
}

$action = $args[0]
if (-not $action) { $action = "status" }

switch ($action.ToLower()) {
    "on"  { Enable-LibraryMode }
    "off" { Disable-LibraryMode }
    "status" { Show-Status }
    "gui" {
        Write-Host "  打开神舟风扇控制面板..." -ForegroundColor Cyan
        $fanPath = "C:\Program Files\WindowsApps\CLEVOCO.504814C03D814_1.16.0.0_x64__6h6z29zh29qx0\FanSpeedSetting\FanSpeedSetting.exe"
        if (Test-Path $fanPath) {
            Start-Process $fanPath
            Write-Host "  已启动 FanSpeedSetting 面板" -ForegroundColor Green
            Write-Host "  建议: 选择 [Silent] 或自定义风扇曲线" -ForegroundColor Yellow
        } else {
            $ccPath = "C:\Program Files\WindowsApps\CLEVOCO.ControlCenter3.0_1.16.7.0_x64__6h6z29zh29qx0\ControlCenter30\ControlCenter30.exe"
            if (Test-Path $ccPath) {
                Start-Process $ccPath
                Write-Host "  已启动 ControlCenter30" -ForegroundColor Green
                Write-Host "  在 [风扇] 页设为 [安静模式]" -ForegroundColor Yellow
            } else {
                Write-Host "  [!] 找不到风扇控制程序" -ForegroundColor Red
            }
        }
    }
    default {
        Write-Host "  用法: .\LibraryMode.ps1 { on | off | status | gui }" -ForegroundColor Yellow
        Show-Status
    }
}
