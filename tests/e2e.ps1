<#
    End-to-end test for ClickOCR.

    Starts the demo window (known text at known coordinates), starts the tray app,
    and drives it through the control pipe:

        grab                              -> the tray app freezes the screen and shows the picker
        pickclient <clientX> <clientY>    -> posts a real WM_LBUTTONDOWN to the picker
        pick <x> <y> <line|word|block>    -> supplies a click point + mode directly

    The clipboard is checked after each grab, so this covers: screen freeze,
    overlay lifecycle, click -> screen coordinate mapping, cropping, OCR,
    word hit-testing and clipboard writing.

    Note: this moves the mouse pointer and briefly shows two windows.
#>
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$exe = Join-Path (Split-Path -Parent $root) 'bin\ClickOCR.exe'
$anchors = Join-Path $env:APPDATA 'ClickOCR\demo-anchors.txt'
$controlFile = Join-Path $env:APPDATA 'ClickOCR\control-last.txt'
$logPath = Join-Path $env:APPDATA 'ClickOCR\log.txt'
$fails = 0

function Send-Command([string]$command) {
    if (Test-Path $controlFile) { Remove-Item $controlFile -Force }
    $p = Start-Process -FilePath $exe -ArgumentList @('--send', $command) -Wait -PassThru
    Start-Sleep -Milliseconds 120
    if (Test-Path $controlFile) { return (Get-Content $controlFile -Raw).Trim() }
    return "<no response>"
}

function Wait-ForFile([string]$path, [int]$timeoutMs = 15000) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        if (Test-Path $path) { Start-Sleep -Milliseconds 150; return $true }
        Start-Sleep -Milliseconds 100
    }
    return $false
}

function Read-Anchors([string]$path) {
    $map = @{}
    foreach ($line in Get-Content $path) {
        if ($line -match '^(?<key>[a-z0-9_]+)=(?<x>-?\d+),(?<y>-?\d+)$') {
            $map[$Matches['key']] = New-Object System.Drawing.Point ([int]$Matches['x']), ([int]$Matches['y'])
        }
    }
    return $map
}

function Check([string]$name, [string]$actual, [scriptblock]$predicate) {
    $ok = $false
    try { $ok = [bool](& $predicate $actual) } catch { $ok = $false }
    if ($ok) {
        Write-Host ("  PASS  {0}" -f $name) -ForegroundColor Green
        if ($actual) { Write-Host ("        -> {0}" -f ($actual -replace "`r?`n", ' | ')) -ForegroundColor DarkGray }
    } else {
        $script:fails++
        Write-Host ("  FAIL  {0}" -f $name) -ForegroundColor Red
        Write-Host ("        -> {0}" -f ($actual -replace "`r?`n", ' | ')) -ForegroundColor DarkGray
    }
}

function Wait-Clipboard([scriptblock]$predicate, [int]$timeoutMs = 6000) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $value = ''
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        try { $value = Get-Clipboard -Raw } catch { $value = '' }
        if ($value -and (& $predicate $value)) { return $value }
        Start-Sleep -Milliseconds 150
    }
    return $value
}

function Seed-Clipboard {
    for ($i = 0; $i -lt 6; $i++) {
        try { Set-Clipboard -Value 'seed'; return } catch { Start-Sleep -Milliseconds 200 }
    }
}

if (-not (Test-Path $exe)) { throw "Build first: $exe is missing." }

$demo = $null
$tray = $null
try {
    Write-Host 'Starting demo window and tray app...' -ForegroundColor Cyan
    Get-Process ClickOCR -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 400
    if (Test-Path $anchors) { Remove-Item $anchors -Force }

    $demo = Start-Process -FilePath $exe -ArgumentList '--demo' -PassThru
    if (-not (Wait-ForFile $anchors)) { throw 'Demo window did not publish its anchors.' }
    $map = Read-Anchors $anchors
    Write-Host ("  anchors: invoice={0} acme={1} total={2} reference={3}" -f $map['invoice'], $map['acme'], $map['total'], $map['reference'])

    $tray = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Milliseconds 2500

    $virtual = [System.Windows.Forms.SystemInformation]::VirtualScreen
    Write-Host ("  virtual screen: {0}" -f $virtual)

    Write-Host ''
    Write-Host 'Test 0: control channel / status' -ForegroundColor Cyan
    $r = Send-Command 'status'
    Check 'status answers' $r { param($t) $t -match 'OK idle' }

    Write-Host ''
    Write-Host 'Test 1: grab + real WM_LBUTTONDOWN on "INV-2026-0042" -> whole line' -ForegroundColor Cyan
    $r = Send-Command 'grab'
    Check 'picker starts' $r { param($t) $t -match 'OK grabbing' }
    Start-Sleep -Milliseconds 700
    Check 'picker is up' (Send-Command 'status') { param($t) $t -match 'picking' }

    Seed-Clipboard
    $client = New-Object System.Drawing.Point (($map['invoice'].X - $virtual.X)), (($map['invoice'].Y - $virtual.Y))
    $r = Send-Command ("pickclient {0} {1}" -f $client.X, $client.Y)
    Check 'click posted' $r { param($t) $t -match 'OK click posted' }
    $text = Wait-Clipboard { param($t) $t -match 'INV-2026-0042' }
    Check 'line mode contains the invoice number' $text { param($t) $t -match 'INV-2026-0042' }
    Check 'line mode starts with the first word'   $text { param($t) $t.Trim().StartsWith('Invoice') }
    Check 'picker closed afterwards' (Send-Command 'status') { param($t) $t -match 'OK idle' }

    Write-Host ''
    Write-Host 'Test 2: pick at "Acme" in word mode' -ForegroundColor Cyan
    $r = Send-Command 'grab'
    Start-Sleep -Milliseconds 700
    Seed-Clipboard
    $r = Send-Command ("pick {0} {1} word" -f $map['acme'].X, $map['acme'].Y)
    Check 'pick accepted' $r { param($t) $t -match 'OK picked' }
    $text = Wait-Clipboard { param($t) $t.Trim() -eq 'Acme' }
    Check 'word mode returns exactly "Acme"' $text { param($t) $t.Trim() -eq 'Acme' }

    Write-Host ''
    Write-Host 'Test 3: pick at the third line in block mode' -ForegroundColor Cyan
    $r = Send-Command 'grab'
    Start-Sleep -Milliseconds 700
    Seed-Clipboard
    $r = Send-Command ("pick {0} {1} block" -f $map['reference'].X, $map['reference'].Y)
    $text = Wait-Clipboard { param($t) $t -match 'Acme Corp' }
    Check 'block mode contains the third line' $text { param($t) $t -match '9f2c\s*-\s*77b1\s*-\s*alpha' }
    Check 'block mode contains the first line' $text { param($t) $t -match 'Acme Corp' }
    Check 'block mode keeps multiple lines'    $text { param($t) ($t -split "`r?`n").Count -ge 3 }

    Write-Host ''
    if ($fails -eq 0) {
        Write-Host 'E2E RESULT: PASS' -ForegroundColor Green
    } else {
        Write-Host ("E2E RESULT: FAIL ({0} checks failed)" -f $fails) -ForegroundColor Red
        Write-Host ''
        Write-Host '--- app log (tail) ---' -ForegroundColor Yellow
        if (Test-Path $logPath) { Get-Content $logPath -Tail 25 }
    }
}
finally {
    Start-Sleep -Milliseconds 300
    foreach ($p in @($tray, $demo)) {
        if ($p -and -not $p.HasExited) { try { $p.Kill() } catch { } }
    }
    Get-Process ClickOCR -ErrorAction SilentlyContinue | Stop-Process -Force
}

if ($fails -ne 0) { exit 1 }
exit 0

