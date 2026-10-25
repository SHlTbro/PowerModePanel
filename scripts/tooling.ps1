param([Parameter(Mandatory = $true)][ValidateSet('check-runtime', 'build', 'test', 'smoke')][string]$Action)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin = Join-Path $projectRoot 'bin'
$source = Join-Path $projectRoot 'PowerModePanel.cs'
$references = @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Management.dll')
function Require-Compiler {
    if (-not [Environment]::Is64BitOperatingSystem -or -not [Environment]::Is64BitProcess) { throw 'Use 64-bit Windows and 64-bit Windows PowerShell.' }
    if ($PSVersionTable.PSEdition -ne 'Desktop' -or $PSVersionTable.PSVersion -lt [version]'5.1') { throw 'Use Windows PowerShell 5.1.' }
    if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x x64 compiler is missing. Install the Windows .NET Framework 4.8 feature/runtime.' }
}
function Compile([string[]]$Options) {
    & $compiler /nologo @references @Options
    if ($LASTEXITCODE -ne 0) { throw "C# compilation failed ($LASTEXITCODE)." }
}
try {
    Require-Compiler
    if ($Action -eq 'check-runtime') {
        Write-Output 'PASS Windows x64, Windows PowerShell 5.1, .NET Framework compiler'
        $oem = Join-Path ${env:ProgramFiles(x86)} 'ControlCenter\InsydeDCHU.dll'
        if (Test-Path -LiteralPath $oem) { Write-Output 'AVAILABLE OEM Control Center DLL (hardware compatibility still requires verification)' }
        else { Write-Output 'OPTIONAL MISSING OEM Control Center DLL: fan control will be unavailable' }
        if (Get-Command nvidia-smi -ErrorAction SilentlyContinue) { Write-Output 'AVAILABLE NVIDIA nvidia-smi' }
        else { Write-Output 'OPTIONAL MISSING nvidia-smi: GPU metrics will display --' }
        exit 0
    }
    New-Item -ItemType Directory -Path $bin -Force | Out-Null
    if ($Action -eq 'build') {
        Compile @('/target:winexe', '/platform:x64', "/out:$bin\PowerModePanel.exe", "/win32manifest:$projectRoot\PowerModePanel.exe.manifest", $source)
        foreach ($file in @('LibraryMode.ps1', 'PerformanceCoolMode.ps1')) {
            Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination (Join-Path $bin $file) -Force
        }
        Write-Output 'PASS build: bin\PowerModePanel.exe (administrator manifest)'
    }
    elseif ($Action -eq 'test') {
        Compile @('/target:exe', '/platform:x64', '/main:FanControllerTests', "/out:$bin\FanControllerTests.exe", $source, (Join-Path $projectRoot 'tests\FanControllerTests.cs'))
        & (Join-Path $bin 'FanControllerTests.exe')
        if ($LASTEXITCODE -ne 0) { throw "Isolated tests failed ($LASTEXITCODE)." }
    }
    elseif ($Action -eq 'smoke') {
        $app = Join-Path $bin 'PowerModePanel.exe'
        if (-not (Test-Path -LiteralPath $app)) { throw 'Build the app first with scripts\build.cmd.' }
        Compile @('/target:exe', '/platform:x64', "/reference:$app", "/out:$bin\ShowcaseSmoke.exe", (Join-Path $PSScriptRoot 'ShowcaseSmoke.cs'))
        $artifactDir = Join-Path $projectRoot 'artifacts'
        New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
        & (Join-Path $bin 'ShowcaseSmoke.exe') $artifactDir
        if ($LASTEXITCODE -ne 0) { throw "Read-only UI smoke failed ($LASTEXITCODE)." }
    }
    exit 0
}
catch { Write-Error $_; exit 1 }
