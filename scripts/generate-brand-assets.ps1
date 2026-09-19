[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Drawing

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resourceDirectory = Join-Path $projectRoot 'duokai\Resources'
$docsAssetDirectory = Join-Path $projectRoot 'docs\assets'
$iconPath = Join-Path $projectRoot 'duokai\favicon.ico'
$resourcePngPath = Join-Path $resourceDirectory 'WeichuangLogo.png'
[xml]$centralVersionFile = Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props')
$version = [string]$centralVersionFile.Project.PropertyGroup.WechatDuokaiVersion
$docsPngPath = Join-Path $docsAssetDirectory ("weichuang-logo-v" + $version + '.png')

New-Item -ItemType Directory -Path $resourceDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $docsAssetDirectory -Force | Out-Null

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-BrandBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

        $scale = $Size / 1024.0
        $green = [Drawing.ColorTranslator]::FromHtml('#20B26B')
        $blue = [Drawing.ColorTranslator]::FromHtml('#3D8EF7')
        $mint = [Drawing.ColorTranslator]::FromHtml('#62D6C5')
        $greenDot = [Drawing.ColorTranslator]::FromHtml('#F8FFFC')
        $blueDot = [Drawing.ColorTranslator]::FromHtml('#F7FBFF')

        $greenPath = New-RoundedRectanglePath (116 * $scale) (126 * $scale) (522 * $scale) (522 * $scale) (136 * $scale)
        $bluePath = New-RoundedRectanglePath (386 * $scale) (364 * $scale) (522 * $scale) (522 * $scale) (136 * $scale)
        try {
            $greenPen = New-Object Drawing.Pen($green, (88 * $scale))
            $bluePen = New-Object Drawing.Pen($blue, (88 * $scale))
            try {
                $greenPen.StartCap = [Drawing.Drawing2D.LineCap]::Round
                $greenPen.EndCap = [Drawing.Drawing2D.LineCap]::Round
                $greenPen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
                $bluePen.StartCap = [Drawing.Drawing2D.LineCap]::Round
                $bluePen.EndCap = [Drawing.Drawing2D.LineCap]::Round
                $bluePen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
                $graphics.DrawPath($greenPen, $greenPath)
                $graphics.DrawPath($bluePen, $bluePath)
            }
            finally {
                $greenPen.Dispose()
                $bluePen.Dispose()
            }
        }
        finally {
            $greenPath.Dispose()
            $bluePath.Dispose()
        }

        $greenDotBrush = New-Object Drawing.SolidBrush($greenDot)
        $blueDotBrush = New-Object Drawing.SolidBrush($blueDot)
        $mintBrush = New-Object Drawing.SolidBrush($mint)
        try {
            foreach ($point in @(@(254, 252), @(362, 252))) {
                $graphics.FillEllipse($greenDotBrush, (($point[0] - 31) * $scale), (($point[1] - 31) * $scale), (62 * $scale), (62 * $scale))
            }
            foreach ($point in @(@(684, 490), @(792, 490))) {
                $graphics.FillEllipse($blueDotBrush, (($point[0] - 31) * $scale), (($point[1] - 31) * $scale), (62 * $scale), (62 * $scale))
            }

            $state = $graphics.Save()
            try {
                $graphics.TranslateTransform((580 * $scale), (560 * $scale))
                $graphics.RotateTransform(-32)
                $graphics.TranslateTransform((-580 * $scale), (-560 * $scale))
                $linkPath = New-RoundedRectanglePath (491 * $scale) (522 * $scale) (178 * $scale) (76 * $scale) (38 * $scale)
                try {
                    $graphics.FillPath($mintBrush, $linkPath)
                }
                finally {
                    $linkPath.Dispose()
                }
            }
            finally {
                $graphics.Restore($state)
            }
        }
        finally {
            $greenDotBrush.Dispose()
            $blueDotBrush.Dispose()
            $mintBrush.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }
    return $bitmap
}

function Convert-BitmapToPngBytes {
    param([Drawing.Bitmap]$Bitmap)
    $stream = New-Object IO.MemoryStream
    try {
        $Bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        Write-Output -NoEnumerate $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

$largeBitmap = New-BrandBitmap 1024
try {
    $largeBitmap.Save($resourcePngPath, [Drawing.Imaging.ImageFormat]::Png)
    $largeBitmap.Save($docsPngPath, [Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $largeBitmap.Dispose()
}

$iconSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()
foreach ($size in $iconSizes) {
    $bitmap = New-BrandBitmap $size
    try {
        [byte[]]$pngFrame = Convert-BitmapToPngBytes $bitmap
        $frames += ,$pngFrame
    }
    finally {
        $bitmap.Dispose()
    }
}

$iconStream = [IO.File]::Open($iconPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
$writer = New-Object IO.BinaryWriter($iconStream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    $offset = 6 + (16 * $frames.Count)
    for ($index = 0; $index -lt $frames.Count; $index++) {
        $size = $iconSizes[$index]
        $encodedSize = if ($size -eq 256) { 0 } else { $size }
        $writer.Write([byte]$encodedSize)
        $writer.Write([byte]$encodedSize)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) {
        $writer.Write($frame)
    }
}
finally {
    $writer.Dispose()
    $iconStream.Dispose()
}

Write-Host "已生成微窗助手 Logo：$resourcePngPath"
Write-Host "已生成 Windows 多尺寸图标：$iconPath"
