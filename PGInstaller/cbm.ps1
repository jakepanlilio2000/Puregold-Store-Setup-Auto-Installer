param(
    [Parameter(Mandatory=$false)]
    [string]$Department = "IT",

    [Parameter(Mandatory=$false)]
    [string]$OwnIP = "192.168.1.101"
)

# Normalize OwnIP to a full HTTP URL
$cleanIp = $OwnIP.Trim()
if (-not ($cleanIp.StartsWith("http://", [System.StringComparison]::OrdinalIgnoreCase) -or $cleanIp.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase))) {
    $cleanIp = "http://$cleanIp"
}
if (-not $cleanIp.EndsWith("/")) {
    $cleanIp = "$cleanIp/"
}

# Define target bookmarks
$bookmarks = @(
    @{ Name = "PCFPRO"; Url = "http://192.168.200.47/pcfpro/index.php" },
    @{ Name = "My Portal"; Url = "http://myportal.puregold.local/index.php/login" },
    @{ Name = "Local Conso"; Url = $cleanIp }
)

if ($Department -eq "IT") {
    $bookmarks += @{ Name = "IT Tools"; Url = "http://192.168.200.107/IT_TOOLS/login.php" }
    $bookmarks += @{ Name = "PurePOS HQ"; Url = "http://192.168.200.107/purepos_hq/" }
}

Write-Host "=== Chrome Bookmark Configuration ==="
Write-Host "Department: $Department"
Write-Host "Own Conso IP: $cleanIp"

# Locate Chrome User Data directories
$localAppData = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)
$chromeBase = Join-Path $localAppData "Google\Chrome\User Data"

$chromeProfiles = @()
if (Test-Path $chromeBase) {
    $defaultDir = Join-Path $chromeBase "Default"
    if (-not (Test-Path $defaultDir)) {
        New-Item -ItemType Directory -Path $defaultDir -Force | Out-Null
    }
    $chromeProfiles += $defaultDir

    $otherProfiles = Get-ChildItem -Path $chromeBase -Directory -Filter "Profile *" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
    if ($otherProfiles) {
        $chromeProfiles += $otherProfiles
    }
} else {
    $defaultDir = Join-Path $chromeBase "Default"
    New-Item -ItemType Directory -Path $defaultDir -Force | Out-Null
    $chromeProfiles += $defaultDir
}

# Close any running Chrome processes to prevent file lock
Get-Process -Name "chrome" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

foreach ($profileDir in $chromeProfiles) {
    $bookmarksFile = Join-Path $profileDir "Bookmarks"
    $jsonObj = $null

    if (Test-Path $bookmarksFile) {
        try {
            $rawJson = Get-Content -Path $bookmarksFile -Raw -Encoding UTF8
            $jsonObj = $rawJson | ConvertFrom-Json
        } catch {
            Write-Host "Warning: Could not parse existing Bookmarks in $profileDir. Creating fresh structure."
            $jsonObj = $null
        }
    }

    if (-not $jsonObj) {
        $jsonObj = [PSCustomObject]@{
            checksum = ""
            roots = [PSCustomObject]@{
                bookmark_bar = [PSCustomObject]@{
                    children = @()
                    date_added = "13300000000000000"
                    date_last_used = "0"
                    date_modified = "13300000000000000"
                    id = "1"
                    name = "Bookmarks bar"
                    type = "folder"
                }
                other = [PSCustomObject]@{
                    children = @()
                    date_added = "13300000000000000"
                    date_last_used = "0"
                    date_modified = "0"
                    id = "2"
                    name = "Other bookmarks"
                    type = "folder"
                }
                synced = [PSCustomObject]@{
                    children = @()
                    date_added = "13300000000000000"
                    date_last_used = "0"
                    date_modified = "0"
                    id = "3"
                    name = "Mobile bookmarks"
                    type = "folder"
                }
            }
            version = 1
        }
    }

    if (-not $jsonObj.roots) {
        $jsonObj | Add-Member -MemberType NoteProperty -Name "roots" -Value ([PSCustomObject]@{})
    }
    if (-not $jsonObj.roots.bookmark_bar) {
        $jsonObj.roots | Add-Member -MemberType NoteProperty -Name "bookmark_bar" -Value ([PSCustomObject]@{
            children = @()
            date_added = "13300000000000000"
            date_last_used = "0"
            date_modified = "13300000000000000"
            id = "1"
            name = "Bookmarks bar"
            type = "folder"
        })
    }

    $existingChildren = [System.Collections.ArrayList]@($jsonObj.roots.bookmark_bar.children)

    # Determine highest numeric ID currently used
    $highestId = 10
    function Find-MaxId($nodes) {
        foreach ($node in $nodes) {
            if ($node.id -match '^\d+$') {
                $val = [int]$node.id
                if ($val -gt $script:highestId) { $script:highestId = $val }
            }
            if ($node.children) {
                Find-MaxId($node.children)
            }
        }
    }

    Find-MaxId($jsonObj.roots.bookmark_bar.children)
    if ($jsonObj.roots.other -and $jsonObj.roots.other.children) { Find-MaxId($jsonObj.roots.other.children) }
    if ($jsonObj.roots.synced -and $jsonObj.roots.synced.children) { Find-MaxId($jsonObj.roots.synced.children) }

    # Upsert each bookmark
    foreach ($bm in $bookmarks) {
        $targetName = $bm.Name
        $targetUrl = $bm.Url

        $existing = $null
        foreach ($child in $existingChildren) {
            if ($child.type -eq "url" -and ($child.name -eq $targetName -or $child.url -eq $targetUrl)) {
                $existing = $child
                break
            }
        }

        if ($existing) {
            $existing.name = $targetName
            $existing.url = $targetUrl
            Write-Host "Updated bookmark: $targetName -> $targetUrl"
        } else {
            $highestId++
            $newEntry = [PSCustomObject]@{
                date_added = "13300000000000000"
                date_last_used = "0"
                id = $highestId.ToString()
                name = $targetName
                type = "url"
                url = $targetUrl
            }
            [void]$existingChildren.Add($newEntry)
            Write-Host "Added bookmark: $targetName -> $targetUrl"
        }
    }

    $jsonObj.roots.bookmark_bar.children = $existingChildren
    $jsonObj.checksum = ""

    $outputJson = $jsonObj | ConvertTo-Json -Depth 32
    [System.IO.File]::WriteAllText($bookmarksFile, $outputJson, [System.Text.Encoding]::UTF8)
    Write-Host "Bookmarks written successfully to: $bookmarksFile"

    # Configure Chrome Preferences to always display the bookmark bar
    $prefFile = Join-Path $profileDir "Preferences"
    if (Test-Path $prefFile) {
        try {
            $prefJson = Get-Content -Path $prefFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if (-not $prefJson.bookmark_bar) {
                $prefJson | Add-Member -MemberType NoteProperty -Name "bookmark_bar" -Value ([PSCustomObject]@{ show_on_all_tabs = $true })
            } else {
                $prefJson.bookmark_bar.show_on_all_tabs = $true
            }
            $updatedPref = $prefJson | ConvertTo-Json -Depth 32
            [System.IO.File]::WriteAllText($prefFile, $updatedPref, [System.Text.Encoding]::UTF8)
        } catch { }
    }
}

