<#
.SYNOPSIS
    M1 기술 검증 측정 실행 스크립트 (SDD 3.12.4 3항 M1 대체 측정, M1-12). PowerShell 5.1 호환.

.DESCRIPTION
    개발 빌드(Builds\Development)로 이 PC 한 대에서 호스트 1개 + 봇 6개를 띄우고, 측정 중간에 7번째 봇을 중도 참가시켜
    서버 처리 예산(Server.Total)·메인 스레드·대역폭·중도 참가 시간·메모리를 모은다.
    결과(CSV·JSON·로그)는 docs\test\perf\raw\M1_<시각>\ 에 모이고, 요약 표를 콘솔과 summary.md 로 낸다.

    판정 기준(SDD 3.12.4): Server.Total 평균 <= 3.0 ms, p99 <= 5.0 ms / 메인 스레드 평균 <= 13 ms /
    참가자 수신 <= 512 kbps, 호스트 송신 <= 4 Mbps / 중도 참가 <= 60 초 / 메모리 10 GB, VRAM 7.5 GB. FPS 는 참고값.

    측정 중에는 Unity 에디터를 닫아 두세요(같은 CPU·GPU·메모리를 써서 결과가 흐려집니다). 짧은 점검(-DurationSeconds 30)은 판정용이 아닙니다.

.PARAMETER BuildDir
    개발 빌드 폴더(기본: <저장소>\src\Builds\Development).
.PARAMETER Mode
    P8 = 호스트 + 봇 6 + 중도 참가 봇 1(기본). P1 = 호스트만(1인).
.PARAMETER DurationSeconds
    측정 시간(초). 모든 처음 봇이 InGame이 된 뒤부터 센다. 기본 300.
.PARAMETER LateJoinAfterSeconds
    중도 참가 봇을 띄울 시점(측정 시작 후 초). 기본 60, 측정 시간의 절반을 넘지 않게 조정된다.
.PARAMETER Bots
    처음에 접속시킬 봇 수(기본 6). 중도 참가 봇 1개가 추가된다(총 7).
.PARAMETER OutRoot
    결과를 모을 폴더(기본: <저장소>\docs\test\perf\raw).
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\perf\RunM1Perf.ps1
    powershell -ExecutionPolicy Bypass -File tools\perf\RunM1Perf.ps1 -Mode P1
    powershell -ExecutionPolicy Bypass -File tools\perf\RunM1Perf.ps1 -DurationSeconds 30
#>
[CmdletBinding()]
param(
    [string]$BuildDir = '',
    [ValidateSet('P8', 'P1')][string]$Mode = 'P8',
    [int]$DurationSeconds = 300,
    [int]$LateJoinAfterSeconds = 60,
    [int]$Bots = 6,
    [string]$OutRoot = ''
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$inv = [System.Globalization.CultureInfo]::InvariantCulture

# ---------- 판정 기준 (SDD 3.12.4 4항, NFR-010~018, NFR-048) ----------
$Limits = @{
    ServerTotalAvgMs = 3.0
    ServerTotalP99Ms = 5.0
    MainThreadAvgMs  = 13.0
    BotRecvKbps      = 512.0
    HostSendKbps     = 4000.0
    JoinSeconds      = 60.0
    ProcessMemoryMb  = 10240.0
    VramMb           = 7680.0
}

# ---------- 경로 ----------
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrEmpty($BuildDir)) { $BuildDir = Join-Path $repoRoot 'src\Builds\Development' }
if ([string]::IsNullOrEmpty($OutRoot)) { $OutRoot = Join-Path $repoRoot 'docs\test\perf\raw' }
$exe = Get-ChildItem -Path $BuildDir -Filter '*.exe' -File | Where-Object { $_.Name -notlike 'UnityCrashHandler*' } | Select-Object -First 1
if ($null -eq $exe) { throw "빌드 폴더에서 실행 파일을 찾지 못했습니다: $BuildDir" }
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$outDir = Join-Path $OutRoot ("M1_" + $stamp)
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$Port = 7770
$gameName = [System.IO.Path]::GetFileNameWithoutExtension($exe.Name)
$cores = [Environment]::ProcessorCount
# 어떤 이유로 스크립트가 중간에 죽어도 게임 프로세스가 남지 않게 하는 안전장치(초).
$backstopSeconds = $DurationSeconds + 420

function Write-Log([string]$text) {
    $line = '[{0}] {1}' -f (Get-Date -Format 'HH:mm:ss'), $text
    Write-Host $line
    Add-Content -Path (Join-Path $outDir 'run.log') -Value $line -Encoding UTF8
}

function Read-SharedText([string]$path) {
    # Unity 가 쓰는 중인 파일도 읽을 수 있게 공유 모드로 연다.
    if (-not (Test-Path -LiteralPath $path)) { return '' }
    $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $sr = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8)
        return $sr.ReadToEnd()
    } finally { $fs.Dispose() }
}

function Percentile([double[]]$values, [double]$p) {
    if ($null -eq $values -or $values.Count -eq 0) { return 0.0 }
    $sorted = $values | Sort-Object
    $rank = [Math]::Ceiling($p / 100.0 * $sorted.Count)
    if ($rank -lt 1) { $rank = 1 }
    if ($rank -gt $sorted.Count) { $rank = $sorted.Count }
    return [double]$sorted[$rank - 1]
}

function Mean([double[]]$values) {
    if ($null -eq $values -or $values.Count -eq 0) { return 0.0 }
    return [double](($values | Measure-Object -Average).Average)
}

function Max([double[]]$values) {
    if ($null -eq $values -or $values.Count -eq 0) { return 0.0 }
    return [double](($values | Measure-Object -Maximum).Maximum)
}

# ---------- 프로세스 시작 ----------
$procs = @{}   # 이름 -> @{ Proc; Dir; Id; Last CPU ... }

function Start-Game([string]$name, [string]$extraArgs) {
    $dir = Join-Path $outDir $name
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $log = Join-Path $dir 'player.log'
    $argLine = ('-logFile "{0}" -perflog "{1}" -autoquit {2} {3}' -f $log, $dir, $backstopSeconds, $extraArgs)
    $p = Start-Process -FilePath $exe.FullName -ArgumentList $argLine -PassThru -WorkingDirectory $exe.DirectoryName
    $procs[$name] = @{ Proc = $p; Dir = $dir; Log = $log; LastCpu = [TimeSpan]::Zero; LastTime = Get-Date; Started = Get-Date }
    Write-Log ("시작: {0} (pid {1})" -f $name, $p.Id)
}

$script:botWindowed = $false

function Start-Bot([string]$name, [int]$id) {
    # 화면 없는 봇(-batchmode -nographics)이 우선이다. 시작하자마자 끝나면(플레이어 시작 단계 오류) 작은 창 모드 봇으로 바꾼다.
    $common = "-bot -transport tugboat -connect 127.0.0.1:$Port -devPlayerId $id"
    if (-not $script:botWindowed) {
        Start-Game $name ("-batchmode -nographics " + $common)
        Start-Sleep -Seconds 6
        if (-not $procs[$name].Proc.HasExited) { return }
        Write-Log ("경고: 화면 없는 봇이 시작하자마자 끝났습니다(종료 코드 {0}). 창 모드 봇(320x180)으로 바꿉니다. 결과에 표시됩니다." -f $procs[$name].Proc.ExitCode)
        $script:botWindowed = $true
    }
    Start-Game $name ("-screen-fullscreen 0 -screen-width 320 -screen-height 180 " + $common)
}

function Stop-Games {
    foreach ($name in @($procs.Keys)) {
        $p = $procs[$name].Proc
        try {
            if (-not $p.HasExited) {
                if ($name -eq 'host') { [void]$p.CloseMainWindow(); [void]$p.WaitForExit(5000) }
                if (-not $p.HasExited) { $p.Kill(); [void]$p.WaitForExit(10000) }
            }
        } catch { Write-Log ("종료 중 오류 {0}: {1}" -f $name, $_.Exception.Message) }
    }
}

function Wait-Until([scriptblock]$condition, [int]$timeoutSeconds, [string]$what) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeoutSeconds) {
        if (& $condition) { return $true }
        Start-Sleep -Milliseconds 500
    }
    Write-Log ("시간 초과: {0} ({1}초)" -f $what, $timeoutSeconds)
    return $false
}

function Find-GpuMemoryMb([int]$processId) {
    # 프로세스별 전용 GPU 메모리(MB). 성능 카운터가 없거나 읽지 못하면 $null (= 측정 불가).
    try {
        $c = Get-Counter -Counter ("\GPU Process Memory(pid_{0}*)\Dedicated Usage" -f $processId) -ErrorAction Stop
        $sum = ($c.CounterSamples | Measure-Object -Property CookedValue -Sum).Sum
        return [double]($sum / 1MB)
    } catch { return $null }
}

$failures = New-Object System.Collections.ArrayList
$gpuSamples = New-Object System.Collections.ArrayList
$procRows = New-Object System.Collections.ArrayList   # name,time_s,cpu_pct_machine,cpu_pct_core,ws_mb,private_mb
$mainWatch = [Diagnostics.Stopwatch]::StartNew()

function Sample-Processes {
    foreach ($name in @($procs.Keys)) {
        $e = $procs[$name]
        $p = $e.Proc
        try {
            if ($p.HasExited) { continue }
            $p.Refresh()
            $now = Get-Date
            $cpu = $p.TotalProcessorTime
            $dt = ($now - $e.LastTime).TotalSeconds
            if ($dt -le 0.2) { continue }
            $core = (($cpu - $e.LastCpu).TotalSeconds / $dt) * 100.0
            $e.LastCpu = $cpu
            $e.LastTime = $now
            [void]$procRows.Add([pscustomobject]@{
                name = $name; time_s = [Math]::Round($mainWatch.Elapsed.TotalSeconds, 1)
                cpu_pct_core = [Math]::Round($core, 1); cpu_pct_machine = [Math]::Round($core / $cores, 1)
                working_set_mb = [Math]::Round($p.WorkingSet64 / 1MB, 1); private_mb = [Math]::Round($p.PrivateMemorySize64 / 1MB, 1)
            })
        } catch { }
    }
}

# =====================================================================
Write-Log ("M1 측정 시작: 모드={0}, 측정 {1}초, 빌드={2}" -f $Mode, $DurationSeconds, $exe.FullName)
Write-Log ("결과 폴더: {0}" -f $outDir)
Write-Log ("PC: {0} 논리 코어 {1}개" -f ((Get-CimInstance Win32_Processor | Select-Object -First 1).Name), $cores)
# Unity 에디터 프로세스만 센다. MCP 연결용 WindowsApps\unity.exe(unity mcp)는
# 실행 파일 경로가 \Editor\Unity.exe로 끝나지 않으므로 에디터로 보지 않는다.
function Test-UnityEditorRunning {
    try {
        $procs = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction Stop
        foreach ($p in $procs) {
            if ($p.ExecutablePath -and $p.ExecutablePath -like '*\Editor\Unity.exe') { return $true }
        }
        return $false
    } catch { return $false }
}

try {
    $gpuNames =(Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name }) -join ', '
    Write-Log ("GPU: {0}" -f $gpuNames)
} catch { $gpuNames = '알 수 없음' }
if (Get-Process -Name $gameName -ErrorAction SilentlyContinue) {
    Write-Log "경고: 이미 실행 중인 $gameName 프로세스가 있습니다. 측정이 흐려질 수 있으니 먼저 끄세요."
}
if (Test-UnityEditorRunning) {
    Write-Log "경고: Unity 에디터가 켜져 있습니다. 판정용 측정에서는 에디터를 닫아야 합니다(짧은 점검은 괜찮음)."
}

$result = [ordered]@{ mode = $Mode; duration_s = $DurationSeconds; started = (Get-Date).ToString('s'); build = $exe.FullName; gpu = $gpuNames; cores = $cores; editor_open = (Test-UnityEditorRunning) }

try {
    # ---- 호스트 ----
    $hostArgs = '-screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -upscale on -transport tugboat -host -devPlayerId 1 -testcreatures 60 -resourcechanges 5000'
    Start-Game 'host' $hostArgs
    $hostLog = $procs['host'].Log
    $hostReady = Wait-Until { (Read-SharedText $hostLog) -match '참가 시간:' -or $procs['host'].Proc.HasExited } 240 '호스트가 게임에 들어감'
    if (-not $hostReady -or $procs['host'].Proc.HasExited) { throw '호스트가 시작하지 못했습니다(player.log 확인).' }
    Write-Log '호스트 준비 완료'

    # ---- 처음 봇 ----
    $botNames = @()
    if ($Mode -eq 'P8') {
        for ($i = 2; $i -le ($Bots + 1); $i++) {
            $name = "bot$i"
            $botNames += $name
            Start-Bot $name $i
            Start-Sleep -Seconds 2   # 월드 생성이 한꺼번에 몰리지 않게 조금씩 띄운다
        }
        $joined = Wait-Until {
            $all = $true
            foreach ($n in $botNames) { if (-not (Get-ChildItem -Path $procs[$n].Dir -Filter '*_join.json' -ErrorAction SilentlyContinue)) { $all = $false } }
            $all
        } 240 '처음 봇이 모두 접속'
        if (-not $joined) { [void]$failures.Add('처음 봇이 시간 안에 모두 접속하지 못함') }
    }
    $measureStart = Get-Date
    $hostCsv = Get-ChildItem -Path $procs['host'].Dir -Filter 'perf_*.csv' | Select-Object -First 1
    $csvOrigin = if ($null -ne $hostCsv) { $hostCsv.CreationTime } else { $procs['host'].Started }
    Write-Log ("측정 시작 (봇 {0}개 접속). 측정 시간 {1}초" -f $botNames.Count, $DurationSeconds)

    # ---- 측정 루프 ----
    $lateName = $null
    $lateAt = [Math]::Min($LateJoinAfterSeconds, [Math]::Floor($DurationSeconds / 2))
    $nextGpu = 0
    while (((Get-Date) - $measureStart).TotalSeconds -lt $DurationSeconds) {
        Sample-Processes
        $t = ((Get-Date) - $measureStart).TotalSeconds
        if ($Mode -eq 'P8' -and $null -eq $lateName -and $t -ge $lateAt) {
            $lateName = 'bot' + ($Bots + 2)
            Write-Log ("중도 참가 봇 시작: {0} (측정 {1:N0}초 지점)" -f $lateName, $t)
            Start-Bot $lateName ($Bots + 2)
        }
        if ($t -ge $nextGpu) {
            $nextGpu = $t + 5
            $g = Find-GpuMemoryMb $procs['host'].Proc.Id
            if ($null -ne $g) { [void]$gpuSamples.Add($g) }
        }
        foreach ($name in @($procs.Keys)) {
            if ($procs[$name].Proc.HasExited -and -not $procs[$name].ContainsKey('ExitNoted')) {
                $procs[$name]['ExitNoted'] = $true
                [void]$failures.Add(("{0} 프로세스가 측정 중 끝남 (종료 코드 {1})" -f $name, $procs[$name].Proc.ExitCode))
                Write-Log ("경고: {0} 가 측정 중 끝났습니다 (종료 코드 {1})" -f $name, $procs[$name].Proc.ExitCode)
            }
        }
        Start-Sleep -Milliseconds 1000
    }
    $measureEnd = Get-Date
    Write-Log '측정 끝, 프로세스를 종료합니다.'
    foreach ($name in @($procs.Keys)) {
        try { $procs[$name]['PeakWs'] = $procs[$name].Proc.PeakWorkingSet64 / 1MB } catch { $procs[$name]['PeakWs'] = 0 }
    }
    if ($Mode -eq 'P8' -and $null -ne $lateName) {
        # 중도 참가가 아직 끝나지 않았으면 조금 더 기다린다(느려서 60초를 넘겼는지 확인하려는 것).
        [void](Wait-Until { [bool](Get-ChildItem -Path $procs[$lateName].Dir -Filter '*_join.json' -ErrorAction SilentlyContinue) } 5 '중도 참가 기록')
    }
}
catch {
    Write-Log ("오류: {0}" -f $_.Exception.Message)
    [void]$failures.Add($_.Exception.Message)
    $measureStart = if ($null -ne $measureStart) { $measureStart } else { Get-Date }
    $measureEnd = Get-Date
}
finally {
    Stop-Games
}

# =====================================================================
# 요약
# =====================================================================
function Read-Csv([string]$dir) {
    $f = Get-ChildItem -Path $dir -Filter 'perf_*.csv' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $f) { return $null }
    # Unity 가 쓰던 파일이라 공유 모드로 읽은 뒤 임시 파일 없이 문자열에서 해석한다.
    return (Read-SharedText $f.FullName) | ConvertFrom-Csv
}

function Col($rows, [string]$name) {
    # 비어 있는 값 없이 숫자 배열로.
    $list = New-Object System.Collections.Generic.List[double]
    foreach ($r in $rows) {
        $v = $r.$name
        if ($null -ne $v -and $v -ne '') { $list.Add([double]::Parse($v, $inv)) }
    }
    return ,$list.ToArray()
}

$summary = New-Object System.Collections.ArrayList
function Add-Row([string]$item, [string]$value, [string]$limit, [string]$verdict) {
    [void]$summary.Add([pscustomobject]@{ 항목 = $item; 값 = $value; 기준 = $limit; 판정 = $verdict })
}
function Judge($ok) { if ($ok) { 'PASS' } else { 'FAIL' } }

# 측정 구간 = 처음 봇이 모두 접속한 시점 + 5초 ~ 측정 끝(호스트 CSV 시각으로 환산)
$hostRows = Read-Csv $procs['host'].Dir
$csvStart = 0; $csvEnd = [double]::MaxValue
if ($null -ne $hostCsv) {
    $csvStart = ($measureStart - $csvOrigin).TotalSeconds + 5
    $csvEnd = ($measureEnd - $csvOrigin).TotalSeconds
}
$win = @()
if ($null -ne $hostRows) {
    $win = @($hostRows | Where-Object { [double]::Parse($_.time_s, $inv) -ge $csvStart -and [double]::Parse($_.time_s, $inv) -le $csvEnd })
}
$hostStats = [ordered]@{}
if ($win.Count -gt 0) {
    $stAvg = Col $win 'Server.Total.avg'; $stP99 = Col $win 'Server.Total.p99'; $stMax = Col $win 'Server.Total.maxcall'
    $main = Col $win 'main_ms_avg'; $gpu = Col $win 'gpu_ms_avg'; $fps = Col $win 'fps_avg'; $low = Col $win 'fps_1pct_low'
    $send = Col $win 'net.send_bytes_per_s'; $creat = Col $win 'creatures.active'; $synced = Col $win 'resources.synced'
    $gc = Col $win 'gc_alloc_bytes'; $peers = Col $win 'net.peers'; $peerSend = Col $win 'net.peer_send_max_bytes_per_s'
    $ws = Col $win 'mem.working_set_mb'; $heap = Col $win 'mem.managed_heap_mb'
    $hostStats = [ordered]@{
        rows = $win.Count
        server_total_avg_ms = Mean $stAvg; server_total_p99_mean_ms = Mean $stP99; server_total_p99_max_ms = Max $stP99; server_total_tick_max_ms = Max $stMax
        main_ms_avg = Mean $main; gpu_ms_avg = Mean $gpu; fps_avg = Mean $fps; fps_1pct_low_mean = Mean $low; fps_1pct_low_min = if ($low.Count) { ($low | Measure-Object -Minimum).Minimum } else { 0 }
        send_kbps_mean = (Mean $send) * 8 / 1000; send_kbps_p95 = (Percentile $send 95) * 8 / 1000; send_kbps_max = (Max $send) * 8 / 1000
        peers_max = Max $peers; peer_send_kbps_max = (Max $peerSend) * 8 / 1000
        creatures_mean = Mean $creat; resources_synced_max = Max $synced; gc_kb_per_s_mean = (Mean $gc) / 1024
        working_set_mb_max = Max $ws; managed_heap_mb_max = Max $heap
    }
}

# 봇별 수신·CPU
$botStats = New-Object System.Collections.ArrayList
foreach ($name in ($procs.Keys | Where-Object { $_ -like 'bot*' } | Sort-Object)) {
    $rows = Read-Csv $procs[$name].Dir
    $recvP95 = 0.0; $recvMax = 0.0; $rtt = 0.0
    if ($null -ne $rows -and $rows.Count -gt 5) {
        $w = @($rows | Select-Object -Skip 5)
        $recv = Col $w 'net.recv_bytes_per_s'
        $recvP95 = (Percentile $recv 95) * 8 / 1000; $recvMax = (Max $recv) * 8 / 1000
        $rtt = Mean (Col $w 'net.rtt_ms')
    }
    $cpu = @($procRows | Where-Object { $_.name -eq $name } | ForEach-Object { [double]$_.cpu_pct_core })
    $joinFile = Get-ChildItem -Path $procs[$name].Dir -Filter '*_join.json' -ErrorAction SilentlyContinue | Select-Object -First 1
    $join = $null
    if ($null -ne $joinFile) { $join = (Get-Content -Raw -Path $joinFile.FullName -Encoding UTF8) | ConvertFrom-Json }
    [void]$botStats.Add([pscustomobject]@{ name = $name; recv_kbps_p95 = $recvP95; recv_kbps_max = $recvMax; rtt_ms = $rtt; cpu_core_pct_mean = (Mean $cpu); join = $join })
}

$hostCpu = @($procRows | Where-Object { $_.name -eq 'host' } | ForEach-Object { [double]$_.cpu_pct_core })
$hostWsPeak = if ($procs['host'].ContainsKey('PeakWs')) { [double]$procs['host']['PeakWs'] } else { 0 }
$gpuMax = if ($gpuSamples.Count -gt 0) { Max ([double[]]$gpuSamples.ToArray()) } else { $null }

# ---- 판정 표 ----
if ($win.Count -eq 0) {
    Add-Row '호스트 성능 CSV' '없음' '-' 'FAIL'
} else {
    $h = $hostStats
    Add-Row ('Server.Total 평균 (ms, {0}초)' -f $h.rows) ($h.server_total_avg_ms.ToString('0.000', $inv)) ('<= ' + $Limits.ServerTotalAvgMs) (Judge ($h.server_total_avg_ms -le $Limits.ServerTotalAvgMs))
    Add-Row 'Server.Total p99 (ms, 초별 p99의 평균)' ($h.server_total_p99_mean_ms.ToString('0.000', $inv)) ('<= ' + $Limits.ServerTotalP99Ms) (Judge ($h.server_total_p99_mean_ms -le $Limits.ServerTotalP99Ms))
    Add-Row 'Server.Total p99 최악의 1초 / 틱 1회 최대 (ms)' ('{0:0.00} / {1:0.00}' -f $h.server_total_p99_max_ms, $h.server_total_tick_max_ms) '참고(틱 상한 6.0)' '-'
    Add-Row '메인 스레드 평균 (ms)' ($h.main_ms_avg.ToString('0.00', $inv)) ('<= ' + $Limits.MainThreadAvgMs) (Judge ($h.main_ms_avg -le $Limits.MainThreadAvgMs))
    Add-Row '호스트 송신 (kbps, 초별 값의 95%)' ($h.send_kbps_p95.ToString('0.0', $inv)) ('<= ' + $Limits.HostSendKbps) (Judge ($h.send_kbps_p95 -le $Limits.HostSendKbps))
    Add-Row '호스트 송신 평균 / 최대 (kbps), 접속자 최대' ('{0:0.0} / {1:0.0}, {2:0}명' -f $h.send_kbps_mean, $h.send_kbps_max, $h.peers_max) '참고' '-'
    Add-Row '호스트 FPS 평균 / 1% Low 평균 / 최저 (참고)' ('{0:0.0} / {1:0.0} / {2:0.0}' -f $h.fps_avg, $h.fps_1pct_low_mean, $h.fps_1pct_low_min) '참고(봇이 같은 CPU를 나눠 씀)' '-'
    Add-Row '호스트 GPU 프레임 (ms, 참고)' ($h.gpu_ms_avg.ToString('0.00', $inv)) '참고(<= 14.5)' '-'
    Add-Row '활동 생물 평균 / 바뀐 자원 / GC 할당(KB/s)' ('{0:0} / {1:0} / {2:0.0}' -f $h.creatures_mean, $h.resources_synced_max, $h.gc_kb_per_s_mean) '조건 확인(60 / 5000)' '-'
}
$maxBotRecv = 0.0
foreach ($b in $botStats) { if ($b.recv_kbps_p95 -gt $maxBotRecv) { $maxBotRecv = $b.recv_kbps_p95 } }
if ($botStats.Count -gt 0) {
    Add-Row '참가자 수신 (kbps, 봇별 95%의 최댓값)' ($maxBotRecv.ToString('0.0', $inv)) ('<= ' + $Limits.BotRecvKbps) (Judge ($maxBotRecv -le $Limits.BotRecvKbps))
}
$lateBot = $botStats | Where-Object { $_.name -eq ('bot' + ($Bots + 2)) } | Select-Object -First 1
if ($Mode -eq 'P8') {
    if ($null -ne $lateBot -and $null -ne $lateBot.join -and $lateBot.join.complete) {
        $j = $lateBot.join
        Add-Row ('중도 참가 시간 (초): 연결 {0} / 월드 생성 {1} / 스냅샷 {2}' -f $j.connect_s, $j.generate_s, $j.snapshot_s) ([string]$j.total_s) ('<= ' + $Limits.JoinSeconds) (Judge ($j.total_s -le $Limits.JoinSeconds))
    } else {
        Add-Row '중도 참가 시간' '기록 없음' ('<= ' + $Limits.JoinSeconds) 'FAIL'
    }
}
$hostMem = [Math]::Max($hostWsPeak, $(if ($hostStats.Contains('working_set_mb_max')) { $hostStats.working_set_mb_max } else { 0 }))
$allMem = $procRows | Group-Object name | ForEach-Object { [pscustomobject]@{ name = $_.Name; peak = ($_.Group | Measure-Object -Property working_set_mb -Maximum).Maximum } }
$worstMem = if ($allMem) { ($allMem | Measure-Object -Property peak -Maximum).Maximum } else { 0 }
$worstMem = [Math]::Max([double]$worstMem, $hostMem)
Add-Row '프로세스 메모리 최대 작업 집합 (MB, 프로세스별 최댓값)' ($worstMem.ToString('0', $inv)) ('<= ' + $Limits.ProcessMemoryMb) (Judge ($worstMem -le $Limits.ProcessMemoryMb))
if ($null -ne $gpuMax) {
    Add-Row 'VRAM 전용 사용량 (MB, 호스트, GPU 성능 카운터)' ($gpuMax.ToString('0', $inv)) ('<= ' + $Limits.VramMb) (Judge ($gpuMax -le $Limits.VramMb))
} else {
    Add-Row 'VRAM 전용 사용량' '측정 불가(GPU 성능 카운터를 읽지 못함)' ('<= ' + $Limits.VramMb) '-'
}
Add-Row '호스트 CPU 점유 (한 코어 기준 %, 평균)' ((Mean $hostCpu).ToString('0.0', $inv)) '참고' '-'
foreach ($b in $botStats) {
    Add-Row ('{0}: CPU(한 코어 %) / 수신 최대(kbps) / RTT(ms)' -f $b.name) ('{0:0.0} / {1:0.0} / {2:0.0}' -f $b.cpu_core_pct_mean, $b.recv_kbps_max, $b.rtt_ms) '참고' '-'
}
foreach ($f in $failures) { Add-Row '실행 문제' $f '-' 'FAIL' }

# ---- 저장 ----
$procRows | Export-Csv -Path (Join-Path $outDir 'procs.csv') -NoTypeInformation -Encoding UTF8
$result['bots_windowed'] = $script:botWindowed
$result['finished'] = (Get-Date).ToString('s')
$result['host'] = $hostStats
$result['bots'] = $botStats
$result['gpu_vram_mb_max'] = $gpuMax
$result['failures'] = $failures
$result['summary'] = $summary
($result | ConvertTo-Json -Depth 8) | Out-File -FilePath (Join-Path $outDir 'summary.json') -Encoding UTF8

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine('# M1 측정 요약')
[void]$md.AppendLine('')
[void]$md.AppendLine(('- 모드 {0}, 측정 {1}초, 시작 {2}' -f $Mode, $DurationSeconds, $result.started))
[void]$md.AppendLine(('- PC: 논리 코어 {0}개, GPU {1}' -f $cores, $gpuNames))
[void]$md.AppendLine(('- Unity 에디터 켜짐: {0}' -f $result.editor_open))
if ($script:botWindowed) { [void]($md.AppendLine('- 주의: 화면 없는 봇이 시작하지 못해 창 모드 봇(320x180)으로 측정했습니다. 호스트 FPS·CPU 참고값이 더 나빠집니다.')) }
if ($DurationSeconds -lt 120) { [void]$md.AppendLine('- 주의: 측정 시간이 짧아 판정용이 아닙니다(점검 실행).') }
[void]$md.AppendLine('')
[void]$md.AppendLine('| 항목 | 값 | 기준 | 판정 |')
[void]$md.AppendLine('|---|---|---|---|')
foreach ($r in $summary) { [void]$md.AppendLine(('| {0} | {1} | {2} | {3} |' -f $r.항목, $r.값, $r.기준, $r.판정)) }
[void]$md.AppendLine('')
[void]$md.AppendLine('원본: host\perf_*.csv, bot*\perf_*.csv(성능), bot*\*_join.json(참가 시간), procs.csv(프로세스 CPU·메모리), */player.log, summary.json')
$md.ToString() | Out-File -FilePath (Join-Path $outDir 'summary.md') -Encoding UTF8

Write-Host ''
$summary | Format-Table -AutoSize -Wrap | Out-String -Width 220 | Write-Host
Write-Log ("결과 폴더: {0}" -f $outDir)

$left = Get-Process -Name $gameName -ErrorAction SilentlyContinue
if ($left) { Write-Log ("경고: 남은 게임 프로세스 {0}개" -f @($left).Count) } else { Write-Log '남은 게임 프로세스 없음' }
