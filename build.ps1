#requires -version 5
<#
    Builds ClickOCR.exe with the C# compiler that ships with Windows,
    referencing the Windows Runtime metadata for Windows.Media.Ocr.
    No .NET SDK, no NuGet, no internet required.
#>
param(
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$sourceDir = Join-Path $root 'src'
$binDir = Join-Path $root 'bin'
$objDir = Join-Path $root 'obj'
New-Item -ItemType Directory -Force -Path $binDir, $objDir | Out-Null

# --- toolchain -------------------------------------------------------------
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

$winmd = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\UnionMetadata' -Recurse -Filter 'Windows.winmd' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notlike '*Facade*' } |
    Sort-Object { [version]($_.Directory.Name) } -Descending |
    Select-Object -First 1
if (-not $winmd) {
    throw 'Windows.winmd not found. Install the Windows 10/11 SDK (Windows SDK for Desktop C++/C# is enough).'
}

$gac = 'C:\Windows\Microsoft.NET\assembly'
function Resolve-Gac([string]$name) {
    $hit = Get-ChildItem $gac -Recurse -Filter "$name.dll" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*GAC_MSIL*' } |
        Select-Object -First 1
    if (-not $hit) { throw "Assembly $name.dll not found in the GAC." }
    return $hit.FullName
}

$facades = @(
    'System.Runtime',
    'System.ObjectModel',
    'System.Runtime.InteropServices',
    'System.Threading.Tasks',
    'System.Collections',
    'System.Runtime.WindowsRuntime',
    'System.Runtime.InteropServices.WindowsRuntime'
) | ForEach-Object { '/r:' + (Resolve-Gac $_) }

# --- icon ------------------------------------------------------------------
$icoPath = Join-Path $objDir 'app.ico'
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 32, 32
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$body = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 112, 200))
$g.FillEllipse($body, 1, 1, 30, 30)
$font = New-Object System.Drawing.Font ('Segoe UI', 17, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$rect = New-Object System.Drawing.RectangleF 0, 1, 32, 32
$g.DrawString('T', $font, [System.Drawing.Brushes]::White, $rect, $sf)
$g.Dispose()
$stream = New-Object System.IO.MemoryStream
$bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $stream.ToArray()
$bmp.Dispose()
$body.Dispose(); $font.Dispose(); $sf.Dispose(); $stream.Dispose()

# PNG-compressed single-image .ico (Vista and later)
$file = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([uint16]0)          # reserved
$writer.Write([uint16]1)          # type: icon
$writer.Write([uint16]1)          # image count
$writer.Write([byte]32)           # width
$writer.Write([byte]32)           # height
$writer.Write([byte]0)            # palette
$writer.Write([byte]0)            # reserved
$writer.Write([uint16]1)          # planes
$writer.Write([uint16]32)         # bpp
$writer.Write([uint32]$png.Length)
$writer.Write([uint32]22)         # offset
$writer.Write($png)
$writer.Close()

# --- compile ---------------------------------------------------------------
$exePath = Join-Path $binDir 'ClickOCR.exe'
$sources = Get-ChildItem (Join-Path $sourceDir '*.cs') | Select-Object -ExpandProperty FullName

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    '/warn:4',
    "/out:$exePath",
    "/win32icon:$icoPath",
    "/resource:$icoPath,app.ico",
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    ('/r:' + $winmd.FullName)
) + $facades + $sources

& $csc $arguments
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $exePath)) { throw 'Build produced no executable.' }

if (-not $Quiet) {
    Write-Host ''
    Write-Host ("Built {0}  ({1:N0} bytes)" -f $exePath, (Get-Item $exePath).Length) -ForegroundColor Green
    Write-Host ("OCR metadata: {0}" -f $winmd.FullName)
    Write-Host ''
}
