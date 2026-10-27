<#  Verifies the "clickless" mode: pointer over a word + hotkey/command, no click.  #>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$exe = Join-Path (Split-Path -Parent $root) 'bin\ClickOCR.exe'
$anchors = Join-Path $env:APPDATA 'ClickOCR\demo-anchors.txt'
$controlFile = Join-Path $env:APPDATA 'ClickOCR\control-last.txt'
$config = Join-Path $env:APPDATA 'ClickOCR\config.ini'

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Cursor {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
}
"@

function Send-Command([string]$command) {
    if (Test-Path $controlFile) { Remove-Item $controlFile -Force }
    Start-Process -FilePath $exe -ArgumentList @('--send', $command) -Wait | Out-Null
    Start-Sleep -Milliseconds 120
    if (Test-Path $controlFile) { return (Get-Content $controlFile -Raw).Trim() }
    return '<no response>'
}

function Wait-ForFile([string]$path, [int]$timeoutMs = 15000) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        if (Test-Path $path) { Start-Sleep -Milliseconds 150; return $true }
        Start-Sleep -Milliseconds 100
    }
    return $false
}

Get-Process ClickOCR -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
if (Test-Path $anchors) { Remove-Item $anchors -Force }

$demo = Start-Process -FilePath $exe -ArgumentList '--demo' -PassThru
if (-not (Wait-ForFile $anchors)) { throw 'demo anchors missing' }
$map = @{}
foreach ($line in Get-Content $anchors) {
    if ($line -match '^(?<key>[a-z0-9_]+)=(?<x>-?\d+),(?<y>-?\d+)$') {
        $map[$Matches['key']] = New-Object System.Drawing.Point ([int]$Matches['x']), ([int]$Matches['y'])
    }
}

$tray = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 2500

$point = $map['total']
[void][Cursor]::SetCursorPos($point.X, $point.Y)
Start-Sleep -Milliseconds 300

try { Set-Clipboard -Value 'seed' } catch { }

$response = Send-Command 'grabcursor'
Write-Host "grabcursor -> $response"
Start-Sleep -Milliseconds 2500
$text = Get-Clipboard -Raw
Write-Host ("clipboard  -> {0}" -f ($text -replace "`r?`n", ' | '))

$ok = $text -match '1,299'
Write-Host ''
if ($ok) { Write-Host 'CLICKLESS RESULT: PASS' -ForegroundColor Green }
else { Write-Host 'CLICKLESS RESULT: FAIL' -ForegroundColor Red }

Stop-Process -Id $tray.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $demo.Id -Force -ErrorAction SilentlyContinue
Get-Process ClickOCR -ErrorAction SilentlyContinue | Stop-Process -Force
if (-not $ok) { exit 1 }
exit 0
