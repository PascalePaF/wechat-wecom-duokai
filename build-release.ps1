[CmdletBinding()]
param(
    [string]$Version = '1.0.1'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$sourceRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$artifactParent = [IO.Path]::GetFullPath((Join-Path $sourceRoot 'artifacts'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $artifactParent ("V" + $Version)))
$versionSuffix = 'v' + $Version

if (-not $artifactRoot.StartsWith($artifactParent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw '发布目录不在项目 artifacts 目录中，已停止构建。'
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = $null
if (Test-Path -LiteralPath $vswhere) {
    $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (-not $msbuild) {
    $knownMsBuild = 'D:\Visual Studio\program\MSBuild\Current\Bin\MSBuild.exe'
    if (Test-Path -LiteralPath $knownMsBuild) {
        $msbuild = $knownMsBuild
    }
}
if (-not $msbuild) {
    throw '未找到 Visual Studio MSBuild。请安装 Visual Studio 2019 或更高版本及 .NET Framework 4.8 开发工具。'
}

& $msbuild (Join-Path $sourceRoot 'duokai.sln') /t:Rebuild /p:Configuration=Release /m /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Release 编译失败，退出码：$LASTEXITCODE"
}

& (Join-Path $sourceRoot 'tests\bin\Release\WechatDuokai.Tests.exe')
if ($LASTEXITCODE -ne 0) {
    throw "自动化验证失败，退出码：$LASTEXITCODE"
}

if (Test-Path -LiteralPath $artifactRoot) {
    $resolvedArtifactRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $artifactRoot).Path)
    if (-not $resolvedArtifactRoot.StartsWith($artifactParent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw '现有发布目录未通过路径校验，拒绝清理。'
    }
    $artifactItem = Get-Item -LiteralPath $resolvedArtifactRoot -Force
    if (($artifactItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw '现有发布目录是目录联接或符号链接，拒绝递归清理。'
    }
    Remove-Item -LiteralPath $resolvedArtifactRoot -Recurse -Force
}

$installerDirectory = Join-Path $artifactRoot 'installer'
$portableDirectory = Join-Path $artifactRoot ("portable\wechat_duokai-portable-" + $versionSuffix)
New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $portableDirectory -Force | Out-Null

Set-Content -LiteralPath (Join-Path $artifactRoot '.wechat-duokai-artifacts') -Value 'wechat-duokai-artifacts:6f5919ee-24d0-43df-8de5-6b0558d79a98' -Encoding UTF8 -NoNewline
Set-Content -LiteralPath (Join-Path $portableDirectory '.wechat-duokai-portable') -Value 'wechat-duokai-portable:c4ad4e76-7449-4f7b-9ab7-5b9379dd3631' -Encoding UTF8 -NoNewline

$appOutput = Join-Path $sourceRoot 'duokai\bin\Release'
$setupOutput = Join-Path $sourceRoot ("installer\bin\Release\wechat_duokai-setup-" + $versionSuffix + '.exe')
$setupTarget = Join-Path $installerDirectory ("wechat_duokai-setup-" + $versionSuffix + '.exe')
$cleanupTarget = Join-Path $installerDirectory ("wechat_duokai-cleanup-" + $versionSuffix + '.exe')

Copy-Item -LiteralPath $setupOutput -Destination $setupTarget
Copy-Item -LiteralPath $setupOutput -Destination $cleanupTarget
Copy-Item -LiteralPath (Join-Path $appOutput 'duokai.exe') -Destination (Join-Path $portableDirectory 'wechat_duokai.exe')
Copy-Item -LiteralPath (Join-Path $appOutput 'duokai.exe.config') -Destination (Join-Path $portableDirectory 'wechat_duokai.exe.config')
Copy-Item -LiteralPath $setupOutput -Destination (Join-Path $portableDirectory ("wechat_duokai-cleanup-" + $versionSuffix + '.exe'))
Copy-Item -LiteralPath (Join-Path $sourceRoot 'LICENSE') -Destination (Join-Path $portableDirectory 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'README.md') -Destination (Join-Path $portableDirectory 'README.md')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'SECURITY-AUDIT.md') -Destination (Join-Path $portableDirectory 'SECURITY-AUDIT.md')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'packaging\PORTABLE-README.txt') -Destination (Join-Path $portableDirectory '使用说明.txt')
$hardcodedPathReport = Join-Path $sourceRoot 'docs\旧版EXE硬编码路径说明与风险评估报告.txt'
if (Test-Path -LiteralPath $hardcodedPathReport) {
    Copy-Item -LiteralPath $hardcodedPathReport -Destination (Join-Path $portableDirectory '安全说明-旧版EXE硬编码路径报告.txt')
}
$completeSecurityReport = Join-Path $sourceRoot 'docs\微信企业微信多开助手_V1.0.1_完整安全审计报告.txt'
if (Test-Path -LiteralPath $completeSecurityReport) {
    Copy-Item -LiteralPath $completeSecurityReport -Destination (Join-Path $portableDirectory '完整安全审计与卡巴斯基告警调查报告.txt')
}

$portableZip = Join-Path (Split-Path -Parent $portableDirectory) ("wechat_duokai-portable-" + $versionSuffix + '.zip')
Compress-Archive -Path (Join-Path $portableDirectory '*') -DestinationPath $portableZip -CompressionLevel Optimal -Force

$hashTargets = @($setupTarget, $cleanupTarget, $portableZip, (Join-Path $portableDirectory 'wechat_duokai.exe'))
$hashLines = foreach ($file in $hashTargets) {
    $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
    $relativePath = $file.Substring($artifactRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar).Replace('\', '/')
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $relativePath
}
$hashLines | Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Encoding ASCII

$manifest = @(
    "Product=$Version",
    'Framework=.NET Framework 4.8',
    'Platform=Windows 10/11 x64',
    ("Installer=installer/wechat_duokai-setup-" + $versionSuffix + '.exe'),
    ("Cleanup=installer/wechat_duokai-cleanup-" + $versionSuffix + '.exe'),
    ("Portable=portable/wechat_duokai-portable-" + $versionSuffix + '.zip'),
    'Signed=False'
)
$manifest | Set-Content -LiteralPath (Join-Path $artifactRoot 'release-manifest.txt') -Encoding UTF8

Write-Host "发布完成：$artifactRoot"
