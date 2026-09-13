[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runner = Join-Path $projectRoot ("tests\bin\" + $Configuration + "\net48\WechatDuokai.Tests.exe")
$output = Join-Path $projectRoot 'docs\assets'

if (-not (Test-Path -LiteralPath $runner)) {
    throw '尚未找到界面测试程序。请先构建 Release。'
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
$scenarios = @(
    @{ Window='main'; Theme='Light'; Width=960; Height=640; File='v1.0.3-main-light-960x640.png' },
    @{ Window='main'; Theme='Dark'; Width=960; Height=640; File='v1.0.3-main-dark-960x640.png' },
    @{ Window='main'; Theme='Light'; Width=680; Height=520; File='v1.0.3-main-compact-light-680x520.png' },
    @{ Window='main'; Theme='Dark'; Width=1280; Height=800; File='v1.0.3-main-wide-dark-1280x800.png' },
    @{ Window='main'; Theme='Light'; Width=1920; Height=1080; File='v1.0.3-main-fullhd-light-1920x1080.png' },
    @{ Window='installer'; Theme='Light'; Width=780; Height=570; File='v1.0.3-installer-light.png' },
    @{ Window='installer'; Theme='Dark'; Width=780; Height=570; File='v1.0.3-installer-dark.png' },
    @{ Window='installer-complete'; Theme='Light'; Width=780; Height=570; File='v1.0.3-installer-complete-light.png' },
    @{ Window='uninstaller'; Theme='Light'; Width=810; Height=680; File='v1.0.3-uninstaller-light.png' },
    @{ Window='uninstaller'; Theme='Dark'; Width=810; Height=680; File='v1.0.3-uninstaller-dark.png' }
)

foreach ($scenario in $scenarios) {
    $target = Join-Path $output $scenario.File
    & $runner --snapshot $scenario.Window $target $scenario.Theme $scenario.Width $scenario.Height
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $target)) {
        throw "界面截图失败：$($scenario.File)"
    }
}

Write-Host "已生成 $($scenarios.Count) 个 V1.0.3 界面回归样本：$output"
