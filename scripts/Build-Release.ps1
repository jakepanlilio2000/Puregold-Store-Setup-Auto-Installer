param(
    [string]$AssetsPath = 'F:\IT Files\PG Installer (Zone 11)\assets',
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\publish'),
    [switch]$AssetsOnly,
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$source = (Resolve-Path -LiteralPath $AssetsPath).Path
$output = [IO.Path]::GetFullPath($OutputPath)
if ($output.Equals($source, [StringComparison]::OrdinalIgnoreCase) -or $output.StartsWith($source.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The output directory must be outside the asset source.'
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
if (-not $AssetsOnly) {
    $buildArguments = @('publish', (Join-Path $projectRoot 'PGInstaller\PGInstaller.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-o', $output)
    if ($Offline) { $buildArguments += @('-p:PGOfflineBuild=true', '-p:NuGetAudit=false', "-p:RestoreSources=$env:USERPROFILE\.nuget\packages") }
    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
}
$projectXml = [xml](Get-Content (Join-Path $projectRoot 'PGInstaller\PGInstaller.csproj') -Raw)
$releaseVersion = [string]$projectXml.Project.PropertyGroup.Version
$sourceFiles = @(Get-ChildItem -LiteralPath $source -Recurse -File -Force | Sort-Object FullName)
Write-Output "Hashing $($sourceFiles.Count) source assets for release $releaseVersion..."
$manifestFiles = @($sourceFiles | ForEach-Object {
    $relative = $_.FullName.Substring($source.TrimEnd('\').Length + 1).Replace('\', '/')
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    $fileVersion = ''
    if ($_.Extension -in @('.exe', '.dll')) { $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($_.FullName).FileVersion }
    if ([string]::IsNullOrWhiteSpace($fileVersion)) { $fileVersion = 'sha256:' + $hash.Substring(0, 16) }
    @{ Path = $relative; Size = $_.Length; Sha256 = $hash; Version = $fileVersion }
})
$manifest = @{ ReleaseVersion = $releaseVersion; Files = $manifestFiles } | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $output 'assets.manifest.json'), $manifest, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'RELEASE-NOTES.md') -Destination $output -Force
$sevenZip = Join-Path $projectRoot 'PGInstaller\7z.exe'
Copy-Item -LiteralPath $sevenZip -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'PGInstaller\7z.dll') -Destination $output -Force
$pending = Join-Path $output ('assets-' + [guid]::NewGuid().ToString('N') + '.zip')
$archive = Join-Path $output 'assets.zip'
Push-Location -LiteralPath (Split-Path $source -Parent)
try {
    & $sevenZip a -tzip $pending (Split-Path $source -Leaf) '-ppw@1234' -mem=AES256 -mx=1 -y -bsp0
    if ($LASTEXITCODE -ne 0) { throw 'Asset compression failed.' }
    & $sevenZip t $pending '-ppw@1234' -bsp0
    if ($LASTEXITCODE -ne 0) { throw 'Asset archive verification failed.' }
    Move-Item -LiteralPath $pending -Destination $archive -Force
    $checksums = @(Get-ChildItem -LiteralPath $output -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
    })
    [IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), [string[]]$checksums, [Text.UTF8Encoding]::new($false))
    Write-Output "Verified asset archive: $archive"
}
finally {
    Pop-Location
}
