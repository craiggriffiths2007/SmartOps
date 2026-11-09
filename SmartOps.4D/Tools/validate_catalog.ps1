$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$catalogPath = Join-Path $projectRoot 'Project\Sources\catalog.4DCatalog'
$dtdPath = Join-Path $PSScriptRoot 'dtd\base.dtd'
$temporaryXml = Join-Path ([System.IO.Path]::GetTempPath()) ('GlassOps4D-catalog-' + [Guid]::NewGuid().ToString('N') + '.xml')
$reader = $null
try {
    $catalogText = [System.IO.File]::ReadAllText($catalogPath)
    $dtdUri = ([System.Uri]$dtdPath).AbsoluteUri
    $catalogText = $catalogText.Replace('http://www.4d.com/dtd/2007/base.dtd', $dtdUri)
    [System.IO.File]::WriteAllText($temporaryXml, $catalogText, [System.Text.UTF8Encoding]::new($false))
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Parse
    $settings.ValidationType = [System.Xml.ValidationType]::DTD
    $settings.XmlResolver = [System.Xml.XmlUrlResolver]::new()
    $settings.add_ValidationEventHandler({ param($sender, $eventArgs) throw $eventArgs.Message })
    $reader = [System.Xml.XmlReader]::Create($temporaryXml, $settings)
    while ($reader.Read()) {}
    Write-Output 'PASS: catalog validates against the official 4D XML DTD (not a native compilation test).'
} finally {
    if ($null -ne $reader) { $reader.Dispose() }
    if (Test-Path -LiteralPath $temporaryXml) { Remove-Item -LiteralPath $temporaryXml }
}
