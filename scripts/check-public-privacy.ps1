[CmdletBinding()]
param([string]$ArtifactsPath)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$versions = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props')
$version = [string]$versions.Project.PropertyGroup.WechatDuokaiVersion
$script:issues = [Collections.Generic.List[string]]::new()
$script:checked = 0
$privatePath = '(?i)\b[a-z]:\\(?:Users\\(?!Public\\|Default\\|runneradmin\\)|Acode\\|A IDM download path\\|中文安装地址\\|EnglishDownloadInstallPath\\|Visual Studio\\)'
$credential = '(?:gh[pousr]_[a-zA-Z0-9]{30,}|github_pat_[a-zA-Z0-9_]{50,}|AKIA[A-Z0-9]{16}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)'

function Test-PublicBytes([string]$name, [byte[]]$bytes) {
    $script:checked++
    if ($name -match '(?i)(^|/|\\)(\.env(?:\..*)?|settings\.ini(?:\.bak)?|theme\.ini|diagnostic-[^/\\]+\.txt)$|\.(?:pfx|p12|pem|suo|pdb|dmp)$') {
        $script:issues.Add("禁止发布的本地数据或密钥文件：$name")
    }
    if ($name -match '(?i)\.(png|ico|jpg|jpeg)$') { return }
    $texts = @([Text.Encoding]::UTF8.GetString($bytes))
    if ($name -match '(?i)\.(exe|dll)$') { $texts += [Text.Encoding]::Unicode.GetString($bytes) }
    foreach ($text in $texts) {
        $normalized = $text.Replace('\\', '\')
        if ([regex]::IsMatch($normalized, $privatePath)) { $script:issues.Add("未脱敏的本机路径：$name") }
        if ([regex]::IsMatch($text, $credential)) { $script:issues.Add("疑似访问凭据：$name") }
    }
}

$tracked = & git -c core.quotepath=false -C $root ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw '无法枚举待发布源码。' }
foreach ($relative in $tracked) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    if ($relative -match '^docs/assets/v[0-9].*\.png$' -and
        $relative -notmatch '-logo-' -and -not $relative.StartsWith('docs/assets/v1.1.8-') -and
        -not $relative.StartsWith('docs/assets/v1.1.9-') -and
        -not $relative.StartsWith('docs/assets/v' + $version + '-')) {
        $script:issues.Add("旧的非演示界面图片不再进入当前发布树：$relative")
    }
    Test-PublicBytes $relative ([IO.File]::ReadAllBytes($path))
}

if ($ArtifactsPath) {
    Add-Type -AssemblyName System.IO.Compression
    $drop = (Resolve-Path -LiteralPath $ArtifactsPath).Path
    foreach ($file in Get-ChildItem -LiteralPath $drop -File -Recurse -Force) {
        $relative = $file.FullName.Substring($drop.Length).TrimStart('\', '/')
        if ($file.Extension -ne '.zip') {
            Test-PublicBytes ('artifact/' + $relative) ([IO.File]::ReadAllBytes($file.FullName))
            continue
        }
        $stream = [IO.File]::OpenRead($file.FullName)
        try {
            $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read)
            try {
                $total = 0L
                foreach ($entry in $zip.Entries) {
                    $total += $entry.Length
                    if ($entry.Length -gt 20MB -or $total -gt 150MB) { throw '发布 ZIP 超出扫描大小限制。' }
                    if ($entry.FullName.EndsWith('/')) { continue }
                    $input = $entry.Open()
                    $memory = [IO.MemoryStream]::new()
                    try { $input.CopyTo($memory); Test-PublicBytes ($relative + '::' + $entry.FullName) $memory.ToArray() }
                    finally { $input.Dispose(); $memory.Dispose() }
                }
            } finally { $zip.Dispose() }
        } finally { $stream.Dispose() }
    }
}
if ($script:issues.Count) {
    $script:issues | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
    throw '公开资料隐私检查未通过；报告不输出实际凭据值。'
}
"公开资料隐私检查通过：$script:checked 个文件或 ZIP 条目。"
