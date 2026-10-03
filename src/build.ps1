# 포모도로 타이머.exe 를 다시 만든다 (src 폴더에서: powershell -File build.ps1)
Add-Type -AssemblyName System.Drawing
$here = $PSScriptRoot
$root = Split-Path $here

# 1) 아이콘 (토마토) 생성 → PNG 기반 .ico
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.FillEllipse((New-Object System.Drawing.Drawing2D.LinearGradientBrush([System.Drawing.Point]::new(0,40), [System.Drawing.Point]::new(256,256), [System.Drawing.Color]::FromArgb(255,107,107), [System.Drawing.Color]::FromArgb(255,159,107))), 24, 60, 208, 188)
$g.FillPie((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(47,212,160))), 88, 30, 80, 70, 200, 140)
$g.Dispose()
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ico
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]1)
$w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0)
$w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$png.Length); $w.Write([uint32]22)
$w.Write($png); $w.Flush()
$icoPath = Join-Path $here 'app.ico'
[IO.File]::WriteAllBytes($icoPath, $ico.ToArray())

# 2) 컴파일 (Windows에 기본 포함된 .NET Framework 컴파일러 사용)
$csc = Get-ChildItem "$env:WINDIR\Microsoft.NET\Framework64\v4*\csc.exe" | Select-Object -First 1 -ExpandProperty FullName
$out = Join-Path $root '포모도로 타이머.exe'
& $csc /nologo /target:winexe /codepage:65001 /out:$out /win32icon:$icoPath `
    /resource:"$(Join-Path $here 'index.html'),index.html" `
    /reference:System.Windows.Forms.dll (Join-Path $here 'Launcher.cs')
if ($LASTEXITCODE -eq 0) { "빌드 완료: $out" }
