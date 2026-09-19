[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runner = Join-Path $projectRoot ("tests\bin\" + $Configuration + "\net48\WechatDuokai.Tests.exe")
$output = Join-Path $projectRoot 'docs\assets'
[xml]$centralVersionFile = Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props')
$version = [string]$centralVersionFile.Project.PropertyGroup.WechatDuokaiVersion
$prefix = 'v' + $version

if (-not (Test-Path -LiteralPath $runner)) {
    throw '尚未找到界面测试程序。请先构建 Release。'
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
$scenarios = @(
    @{ Window='main'; Theme='Light'; Width=901; Height=513; File=($prefix + '-main-light-901x513.png') },
    @{ Window='main'; Theme='Dark'; Width=901; Height=513; File=($prefix + '-main-dark-901x513.png') },
    @{ Window='main-settings'; Theme='Light'; Width=901; Height=513; File=($prefix + '-settings-light-901x513.png') },
    @{ Window='main-settings'; Theme='Dark'; Width=901; Height=513; File=($prefix + '-settings-dark-901x513.png') },
    @{ Window='main-about'; Theme='Light'; Width=901; Height=513; File=($prefix + '-about-light-901x513.png') },
    @{ Window='main-about'; Theme='Dark'; Width=901; Height=513; File=($prefix + '-about-dark-901x513.png') },
    @{ Window='main'; Theme='Light'; Width=1280; Height=720; File=($prefix + '-main-light-1280x720.png') },
    @{ Window='main'; Theme='Dark'; Width=1920; Height=1080; File=($prefix + '-main-dark-1920x1080.png') },
    @{ Window='main'; Theme='Light'; Width=3440; Height=1392; File=($prefix + '-main-ultrawide-light-3440x1392.png') },
    @{ Window='installer'; Theme='Light'; Width=780; Height=570; File=($prefix + '-installer-light.png') },
    @{ Window='installer'; Theme='Dark'; Width=780; Height=570; File=($prefix + '-installer-dark.png') },
    @{ Window='installer-complete'; Theme='Light'; Width=780; Height=570; File=($prefix + '-installer-complete-light.png') },
    @{ Window='uninstaller'; Theme='Light'; Width=810; Height=680; File=($prefix + '-uninstaller-light.png') },
    @{ Window='uninstaller'; Theme='Dark'; Width=810; Height=680; File=($prefix + '-uninstaller-dark.png') }
)

foreach ($scenario in $scenarios) {
    $target = Join-Path $output $scenario.File
    & $runner --snapshot $scenario.Window $target $scenario.Theme $scenario.Width $scenario.Height
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $target)) {
        throw "界面截图失败：$($scenario.File)"
    }
}

Write-Host "已生成 $($scenarios.Count) 个 V$version 界面回归样本：$output"
