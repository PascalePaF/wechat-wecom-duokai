[CmdletBinding()]
param(
    [string]$Version = '1.1.3'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$sourceRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$artifactParent = [IO.Path]::GetFullPath((Join-Path $sourceRoot 'artifacts'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $artifactParent ("V" + $Version)))
$versionSuffix = 'v' + $Version

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw '版本号必须使用 major.minor.patch 格式。'
}

[xml]$centralVersionFile = Get-Content -LiteralPath (Join-Path $sourceRoot 'Directory.Build.props')
$declaredVersion = [string]$centralVersionFile.Project.PropertyGroup.WechatDuokaiVersion
if ($declaredVersion -ne $Version) {
    throw "构建参数版本 $Version 与 Directory.Build.props 中的 $declaredVersion 不一致。"
}

$expectedManifestVersion = $Version + '.0'
foreach ($manifestPath in @('duokai\app.manifest', 'installer\app.manifest', 'cleanup\app.manifest')) {
    $manifestText = Get-Content -LiteralPath (Join-Path $sourceRoot $manifestPath) -Raw
    if ($manifestText -notmatch ('assemblyIdentity\s+version="' + [Regex]::Escape($expectedManifestVersion) + '"')) {
        throw "清单版本未同步：$manifestPath（应为 $expectedManifestVersion）。"
    }
}

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

& $msbuild (Join-Path $sourceRoot 'duokai.sln') /restore /t:Rebuild /p:Configuration=Release /m /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Release 编译失败，退出码：$LASTEXITCODE"
}

$expectedFileVersion = [Version]($Version + '.0')
$versionedOutputs = @(
    (Join-Path $sourceRoot 'duokai\bin\Release\net48\wechat_duokai.exe'),
    (Join-Path $sourceRoot 'duokai\bin\Release\net48\WechatDuokai.Core.dll'),
    (Join-Path $sourceRoot ("installer\bin\Release\net48\wechat_duokai-setup-v" + $Version + '.exe')),
    (Join-Path $sourceRoot ("cleanup\bin\Release\net48\wechat_duokai-cleanup-v" + $Version + '.exe'))
)
foreach ($outputPath in $versionedOutputs) {
    if (-not (Test-Path -LiteralPath $outputPath)) {
        throw "缺少版本校验目标：$outputPath"
    }
    $actualVersion = [Version]([Diagnostics.FileVersionInfo]::GetVersionInfo($outputPath).FileVersion)
    if ($actualVersion -ne $expectedFileVersion) {
        throw "文件版本未同步：$outputPath（实际 $actualVersion，应为 $expectedFileVersion）。"
    }
}

& (Join-Path $sourceRoot 'tests\bin\Release\net48\WechatDuokai.Tests.exe')
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

$appOutput = Join-Path $sourceRoot 'duokai\bin\Release\net48'
$setupOutput = Join-Path $sourceRoot ("installer\bin\Release\net48\wechat_duokai-setup-" + $versionSuffix + '.exe')
$cleanupOutput = Join-Path $sourceRoot ("cleanup\bin\Release\net48\wechat_duokai-cleanup-" + $versionSuffix + '.exe')
$setupTarget = Join-Path $installerDirectory ("wechat_duokai-setup-" + $versionSuffix + '.exe')
$cleanupTarget = Join-Path $installerDirectory ("wechat_duokai-cleanup-" + $versionSuffix + '.exe')

Copy-Item -LiteralPath $setupOutput -Destination $setupTarget
Copy-Item -LiteralPath $cleanupOutput -Destination $cleanupTarget
Copy-Item -LiteralPath (Join-Path $appOutput 'wechat_duokai.exe') -Destination (Join-Path $portableDirectory 'wechat_duokai.exe')
Copy-Item -LiteralPath (Join-Path $appOutput 'wechat_duokai.exe.config') -Destination (Join-Path $portableDirectory 'wechat_duokai.exe.config')
Copy-Item -LiteralPath (Join-Path $appOutput 'WechatDuokai.Core.dll') -Destination (Join-Path $portableDirectory 'WechatDuokai.Core.dll')
Copy-Item -LiteralPath $cleanupOutput -Destination (Join-Path $portableDirectory ("wechat_duokai-cleanup-" + $versionSuffix + '.exe'))
Copy-Item -LiteralPath (Join-Path $sourceRoot 'LICENSE') -Destination (Join-Path $portableDirectory 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'README.md') -Destination (Join-Path $portableDirectory 'README.md')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'SECURITY-AUDIT.md') -Destination (Join-Path $portableDirectory 'SECURITY-AUDIT.md')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'packaging\PORTABLE-README.txt') -Destination (Join-Path $portableDirectory '使用说明.txt')
$hardcodedPathReport = Join-Path $sourceRoot 'docs\旧版EXE硬编码路径说明与风险评估报告.txt'
if (Test-Path -LiteralPath $hardcodedPathReport) {
    Copy-Item -LiteralPath $hardcodedPathReport -Destination (Join-Path $portableDirectory '安全说明-旧版EXE硬编码路径报告.txt')
}
$completeSecurityReport = Join-Path $sourceRoot ("docs\微窗助手_V" + $Version + "_完整安全审计报告.txt")
if (Test-Path -LiteralPath $completeSecurityReport) {
    Copy-Item -LiteralPath $completeSecurityReport -Destination (Join-Path $portableDirectory '完整安全审计与卡巴斯基告警调查报告.txt')
}
$architectureReport = Join-Path $sourceRoot ("docs\V" + $Version + "-一键更新安全验证报告.md")
if (Test-Path -LiteralPath $architectureReport) {
    Copy-Item -LiteralPath $architectureReport -Destination (Join-Path $portableDirectory '一键更新安全验证报告.md')
}
$releaseNotes = Join-Path $sourceRoot ("docs\release-notes-v" + $Version + ".md")
if (Test-Path -LiteralPath $releaseNotes) {
    Copy-Item -LiteralPath $releaseNotes -Destination (Join-Path $portableDirectory '版本说明.md')
}
$uiRegressionReport = Join-Path $sourceRoot ("docs\V" + $Version + "-UI回归矩阵.md")
if (Test-Path -LiteralPath $uiRegressionReport) {
    Copy-Item -LiteralPath $uiRegressionReport -Destination (Join-Path $artifactRoot ("wechat-duokai-v" + $Version + "-ui-regression-matrix.md"))
}
$projectAuditReport = Join-Path $sourceRoot ("docs\V" + $Version + "-全项目自查报告.md")
if (Test-Path -LiteralPath $projectAuditReport) {
    Copy-Item -LiteralPath $projectAuditReport -Destination (Join-Path $portableDirectory '全项目自查报告.md')
}

# Keep the two release-facing reports beside the binaries as standalone GitHub
# Release assets as well as inside the portable package.
if (Test-Path -LiteralPath $completeSecurityReport) {
    Copy-Item -LiteralPath $completeSecurityReport -Destination (Join-Path $artifactRoot ("wechat-duokai-v" + $Version + "-security-audit.txt"))
}
if (Test-Path -LiteralPath $architectureReport) {
    Copy-Item -LiteralPath $architectureReport -Destination (Join-Path $artifactRoot ("wechat-duokai-v" + $Version + "-one-click-update-validation-report.md"))
}

$portableZip = Join-Path (Split-Path -Parent $portableDirectory) ("wechat_duokai-portable-" + $versionSuffix + '.zip')
Add-Type -AssemblyName System.IO.Compression
$zipStream = [IO.File]::Open($portableZip, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $archive = New-Object IO.Compression.ZipArchive($zipStream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        $fixedZipTime = [DateTimeOffset]::Parse('2026-01-01T00:00:00Z')
        $portableFiles = Get-ChildItem -LiteralPath $portableDirectory -File -Recurse | Sort-Object FullName
        foreach ($file in $portableFiles) {
            $relativeName = $file.FullName.Substring($portableDirectory.Length).TrimStart([IO.Path]::DirectorySeparatorChar).Replace('\', '/')
            $entry = $archive.CreateEntry($relativeName, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $fixedZipTime
            $input = [IO.File]::OpenRead($file.FullName)
            try {
                $output = $entry.Open()
                try {
                    $input.CopyTo($output)
                }
                finally {
                    $output.Dispose()
                }
            }
            finally {
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $zipStream.Dispose()
}

$hashTargets = @(
    $setupTarget,
    $cleanupTarget,
    $portableZip,
    (Join-Path $portableDirectory 'wechat_duokai.exe'),
    (Join-Path $portableDirectory 'WechatDuokai.Core.dll')
)
$hashLines = foreach ($file in $hashTargets) {
    $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
    $relativePath = $file.Substring($artifactRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar).Replace('\', '/')
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $relativePath
}
$hashLines | Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Encoding ASCII

$checksumTarget = Join-Path $artifactRoot 'SHA256SUMS.txt'
$setupHash = (Get-FileHash -LiteralPath $setupTarget -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumHash = (Get-FileHash -LiteralPath $checksumTarget -Algorithm SHA256).Hash.ToLowerInvariant()
$portableHash = (Get-FileHash -LiteralPath $portableZip -Algorithm SHA256).Hash.ToLowerInvariant()
$setupName = [IO.Path]::GetFileName($setupTarget)
$portableName = [IO.Path]::GetFileName($portableZip)
$releaseAssetBase = "https://github.com/PascalePaF/wechat-wecom-duokai/releases/download/$versionSuffix/"
$updateManifest = [ordered]@{
    schemaVersion = 1
    product = 'wechat-duokai'
    version = $Version
    tag = $versionSuffix
    releasePage = "https://github.com/PascalePaF/wechat-wecom-duokai/releases/tag/$versionSuffix"
    minimumUpdaterVersion = '1.1.1'
    setup = [ordered]@{
        name = $setupName
        downloadUrl = $releaseAssetBase + $setupName
        size = (Get-Item -LiteralPath $setupTarget).Length
        sha256 = $setupHash
    }
    checksums = [ordered]@{
        name = 'SHA256SUMS.txt'
        downloadUrl = $releaseAssetBase + 'SHA256SUMS.txt'
        size = (Get-Item -LiteralPath $checksumTarget).Length
        sha256 = $checksumHash
    }
    portable = [ordered]@{
        name = $portableName
        downloadUrl = $releaseAssetBase + $portableName
        size = (Get-Item -LiteralPath $portableZip).Length
        sha256 = $portableHash
    }
}
$updateManifestJson = $updateManifest | ConvertTo-Json -Depth 5
$utf8NoBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $artifactRoot 'update-manifest.json'),
    $updateManifestJson + "`n", $utf8NoBom)

$manifest = @(
    "Product=$Version",
    'Brand=微窗助手',
    'BrandPalette=WeChat-adjacent green plus WeCom-adjacent blue; original dual-window mark',
    'ShellIdentity=Explicit WPF window icon plus matching process/shortcut AppUserModelID and Explorer icon-cache notification',
    'Framework=.NET Framework 4.8',
    'Platform=Windows 10/11 x64',
    'UI=Uniform proportional scaling from 901x513 through maximized layouts; no outer scrollbars',
    'LaunchPolicy=Explicit confirmation after install',
    'ExitAllPolicy=Per-client confirmation; current session and exact verified executable path only; graceful close before force',
    'UpgradePolicy=Prompt before closing exact-path running helper',
    'UpdatePolicy=Explicitly confirmed in-app update from this repository GitHub Release; manual browser download remains available',
    'UpdateVerification=Static Release update manifest is primary and quota-free; GitHub API digest is optional enrichment; SHA256SUMS and downloaded setup must match',
    'UpdateSchedule=Automatic checks are cached for 24 hours, staggered by 30-300 seconds and backed off for 1/6/24 hours; manual checks remain immediate',
    'UpdateNetwork=Valid HTTPS_PROXY or HTTP_PROXY environment URI is honored without persistence; GitHub requests require TLS 1.2; latest static manifest redirect uses HEAD',
    'UpdateRollback=Same-volume staging and per-file backups preserve the previous executable set on failure',
    'InstallerIdentity=Setup and cleanup are separate assemblies',
    'WeComExtendedMode=Temporary registry policy plus exact known mutex release; restore only if temporary state is still owned',
    'RegistryCrashRecovery=Durable pre-write journal under data/recovery; startup restore remains conditional on temporary-state ownership',
    'TargetCounts=One shared persisted 1-10 target for WeChat and WeCom; V1.0.6 split values migrate deterministically',
    'RuntimeStorage=Configuration, theme, diagnostics, recovery and update evidence stay below the application data directory',
    'CI=Fresh GitHub-hosted Windows build, tests, SHA-256 verification and SPDX 2.2 SBOM',
    ("Installer=installer/wechat_duokai-setup-" + $versionSuffix + '.exe'),
    ("Cleanup=installer/wechat_duokai-cleanup-" + $versionSuffix + '.exe'),
    ("Portable=portable/wechat_duokai-portable-" + $versionSuffix + '.zip'),
    'Signed=False'
)
$manifest | Set-Content -LiteralPath (Join-Path $artifactRoot 'release-manifest.txt') -Encoding UTF8

Write-Host "发布完成：$artifactRoot"
