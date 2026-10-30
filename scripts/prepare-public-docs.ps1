[CmdletBinding()]
param([switch]$AllTrackedDocuments, [string]$InputFile, [string]$OutputFile)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function ConvertTo-PublicText([string]$text) {
    $text = $text.Replace($root, '[项目目录]')
    $text = [regex]::Replace($text, '(?i)[a-z]:\\Acode\\[^\\\r\n\t"]+\\(?:wechat_duokai\\){1,2}', '[项目目录]\')
    $text = [regex]::Replace($text, '(?i)[a-z]:\\A IDM download path\\[^\\\r\n\t"]+\\wechat-wecom-duokai-main\\', '[原始下载目录]\')
    $text = [regex]::Replace($text, '(?i)[a-z]:\\中文安装地址\\微信\\Weixin\\', '[微信目录]\')
    $text = [regex]::Replace($text, '(?i)[a-z]:\\中文安装地址\\WXWork\\', '[企业微信目录]\')
    $text = [regex]::Replace($text, '(?i)[a-z]:\\中文安装地址\\wechat_duokai', '[安装目录]')
    $text = [regex]::Replace($text, '(?i)[a-z]:\\中文安装地址\\', '[客户端目录]\')
    $text = [regex]::Replace($text, '(?i)[a-z]:\\EnglishDownloadInstallPath\\QQ\\', '[QQ目录]\')
    $text = [regex]::Replace($text, '(?i)[a-z]:\\Users\\[^\\\r\n\t"]+\\', '[用户目录]\')
    return $text
}

if ($AllTrackedDocuments) {
    $files = & git -c core.quotepath=false -C $root ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw '无法枚举发布文档。' }
    $changed = 0
    foreach ($relative in $files) {
        if ($relative -notmatch '^(docs/.*\.(txt|md)|README\.md|SECURITY-AUDIT\.md)$') { continue }
        $path = Join-Path $root $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $original = [IO.File]::ReadAllText($path)
        $public = ConvertTo-PublicText $original
        if ($relative -match '^docs/kaspersky.*\.txt$' -and $public -ne $original) {
            $public = "; 公开扫描记录：本机路径已脱敏，检测结果与统计保留。`n" + $public
        }
        if ($public -ne $original) {
            [IO.File]::WriteAllText($path, $public, [Text.UTF8Encoding]::new($false))
            $changed++
        }
    }
    "已处理 $changed 份公开文档。"
} else {
    if (-not $InputFile -or -not $OutputFile) { throw '请提供 InputFile 和 OutputFile，或指定 AllTrackedDocuments。' }
    $source = [IO.Path]::GetFullPath($InputFile)
    $destination = [IO.Path]::GetFullPath($OutputFile)
    if ($source -eq $destination) { throw '公开副本不得覆盖原始扫描记录。' }
    $public = "; 公开扫描记录：本机路径已脱敏，检测结果与统计保留。`n" +
              (ConvertTo-PublicText ([IO.File]::ReadAllText($source)))
    [IO.File]::WriteAllText($destination, $public, [Text.UTF8Encoding]::new($false))
    '已生成路径脱敏的公开扫描记录。'
}
