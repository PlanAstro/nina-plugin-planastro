<#
  Builds the manifest.json that NINA's plugin manifest repository expects, from the built DLL's
  assembly metadata and the release zip's SHA256 checksum.
  Check the result with gather.js in the manifest repository before opening the pull request.
#>
param(
  [Parameter(Mandatory)] [string] $Dll,
  [Parameter(Mandatory)] [string] $Archive,
  [Parameter(Mandatory)] [string] $DownloadUrl,
  [Parameter(Mandatory)] [string] $OutFile
)

$ErrorActionPreference = 'Stop'
$asm = [System.Reflection.Assembly]::LoadFile((Resolve-Path $Dll))

function Get-Meta([string] $key) {
  ($asm.GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -eq 'AssemblyMetadataAttribute' -and $_.ConstructorArguments[0].Value -eq $key } |
    Select-Object -First 1).ConstructorArguments[1].Value
}
function Get-Attr([string] $name) {
  ($asm.GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -eq $name } | Select-Object -First 1).ConstructorArguments[0].Value
}
function Split-Version([string] $v) {
  $p = $v.Split('.')
  [ordered]@{ Major = $p[0]; Minor = $p[1]; Patch = $p[2]; Build = $p[3] }
}

$version = $asm.GetName().Version.ToString()
$manifest = [ordered]@{
  Name                      = Get-Attr 'AssemblyTitleAttribute'
  Identifier                = Get-Attr 'GuidAttribute'
  Version                   = Split-Version $version
  Author                    = Get-Attr 'AssemblyCompanyAttribute'
  Homepage                  = Get-Meta 'Homepage'
  Repository                = Get-Meta 'Repository'
  License                   = Get-Meta 'License'
  LicenseURL                = Get-Meta 'LicenseURL'
  ChangelogURL              = Get-Meta 'ChangelogURL'
  Tags                      = (Get-Meta 'Tags').Split(',') | ForEach-Object { $_.Trim() }
  MinimumApplicationVersion = Split-Version (Get-Meta 'MinimumApplicationVersion')
  Descriptions              = [ordered]@{
    ShortDescription = Get-Attr 'AssemblyDescriptionAttribute'
    LongDescription  = Get-Meta 'LongDescription'
    FeaturedImageURL = Get-Meta 'FeaturedImageURL'
    ScreenshotURL    = Get-Meta 'ScreenshotURL'
    AltScreenshotURL = Get-Meta 'AltScreenshotURL'
  }
  Installer                 = [ordered]@{
    URL          = $DownloadUrl
    Type         = 'ARCHIVE'
    Checksum     = (Get-FileHash $Archive -Algorithm SHA256).Hash
    ChecksumType = 'SHA256'
  }
}

$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $OutFile -Encoding utf8
Write-Host "Wrote $OutFile for $($manifest.Name) $version"
