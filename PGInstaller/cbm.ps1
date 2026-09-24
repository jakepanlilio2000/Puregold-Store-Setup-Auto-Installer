param(
    [Parameter(Mandatory=$false)]
    [string]$CsvFilePath = "port# & IP ZONE11.csv",

    [Parameter(Mandatory=$false)]
    [string]$OwnIP = "192.168.1.101",

    [Parameter(Mandatory=$false)]
    [string]$Department = "IT"
)

$ChromeProcessName = "chrome"

Write-Host "Closing Chrome..." -ForegroundColor Yellow
Stop-Process -Name $ChromeProcessName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Clean OwnIP for Purepos Conso URL
$cleanIp = $OwnIP.Trim().TrimEnd('/')
if ($cleanIp.StartsWith("http://", [System.StringComparison]::OrdinalIgnoreCase)) {
    $cleanIp = $cleanIp.Substring(7)
} elseif ($cleanIp.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)) {
    $cleanIp = $cleanIp.Substring(8)
}
if ($cleanIp.EndsWith("/purepos_conso/login", [System.StringComparison]::OrdinalIgnoreCase)) {
    $consoUrl = "http://$cleanIp"
} else {
    $consoUrl = "http://$cleanIp/purepos_conso/login"
}

# Construct Final Bookmark Structure (v3.0 - Shelftag, TPLinux-Kiosk, IT_Tools, Conso List folders removed)
$NewChildren = @(
    @{
        date_added = "13300000000000000"
        id         = "1500"
        name       = "Purepos Conso"
        type       = "url"
        url        = $consoUrl
    },
    @{
        date_added = "13300000000000000"
        id         = "1601"
        name       = "My Portal"
        type       = "url"
        url        = "http://myportal.puregold.local/index.php/login"
    },
    @{
        date_added = "13300000000000000"
        id         = "1602"
        name       = "PCFPROv2"
        type       = "url"
        url        = "http://pcfpro_v2_test.puregold.local/"
    }
)

$localAppData = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)
$chromeBase = Join-Path $localAppData "Google\Chrome\User Data"

$chromeProfiles = @()
$defaultDir = Join-Path $chromeBase "Default"
if (-not (Test-Path $defaultDir)) {
    New-Item -ItemType Directory -Force -Path $defaultDir | Out-Null
}
$chromeProfiles += $defaultDir

if (Test-Path $chromeBase) {
    $otherProfiles = Get-ChildItem -Path $chromeBase -Directory -Filter "Profile *" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
    if ($otherProfiles) {
        $chromeProfiles += $otherProfiles
    }
}

foreach ($profileDir in ($chromeProfiles | Select-Object -Unique)) {
    $BookmarkFile = Join-Path $profileDir "Bookmarks"
    $PreferencesFile = Join-Path $profileDir "Preferences"

    if (-not (Test-Path $BookmarkFile)) {
        Write-Host "Creating new Bookmark database in $profileDir..." -ForegroundColor Cyan
        $Json = [PSCustomObject]@{
            checksum = ""
            roots = [PSCustomObject]@{
                bookmark_bar = [PSCustomObject]@{ children = @(); id = "1"; name = "Bookmarks Bar"; type = "folder" }
                other        = [PSCustomObject]@{ children = @(); id = "2"; name = "Other Bookmarks"; type = "folder" }
                synced       = [PSCustomObject]@{ children = @(); id = "3"; name = "Mobile Bookmarks"; type = "folder" }
            }
            version = 1
        }
    } else {
        Write-Host "Updating existing Bookmarks in $profileDir..." -ForegroundColor Cyan
        try {
            $Json = Get-Content $BookmarkFile -Raw -Encoding UTF8 | ConvertFrom-Json
        } catch {
            $Json = $null
        }
        if (-not $Json) {
            $Json = [PSCustomObject]@{
                checksum = ""
                roots = [PSCustomObject]@{
                    bookmark_bar = [PSCustomObject]@{ children = @(); id = "1"; name = "Bookmarks Bar"; type = "folder" }
                    other        = [PSCustomObject]@{ children = @(); id = "2"; name = "Other Bookmarks"; type = "folder" }
                    synced       = [PSCustomObject]@{ children = @(); id = "3"; name = "Mobile Bookmarks"; type = "folder" }
                }
                version = 1
            }
        }
    }

    if (-not $Json.roots) {
        $Json | Add-Member -MemberType NoteProperty -Name "roots" -Value ([PSCustomObject]@{})
    }
    if (-not $Json.roots.bookmark_bar) {
        $Json.roots | Add-Member -MemberType NoteProperty -Name "bookmark_bar" -Value ([PSCustomObject]@{ children = @(); id = "1"; name = "Bookmarks Bar"; type = "folder" })
    }

    # Clean existing matching children to avoid duplicates on re-run
    $targetFolderNames = @("Shelftag", "TPLinux-Kiosk", "IT_Tools", "Conso List", "Purepos Conso", "My Portal", "PCFPROv2")
    $keptChildren = @()
    if ($Json.roots.bookmark_bar.children) {
        foreach ($c in $Json.roots.bookmark_bar.children) {
            if ($targetFolderNames -notcontains $c.name) {
                $keptChildren += $c
            }
        }
    }

    $Json.roots.bookmark_bar.children = @($keptChildren) + @($NewChildren)
    $Json.checksum = ""

    $outputJson = $Json | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($BookmarkFile, $outputJson, [System.Text.Encoding]::UTF8)

    # Enforce Bookmark Bar visibility
    Write-Host "Enforcing Bookmark Bar visibility in $profileDir..." -ForegroundColor Cyan
    $PrefsJson = if (Test-Path $PreferencesFile) {
        try { Get-Content $PreferencesFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch { [PSCustomObject]@{} }
    } else {
        [PSCustomObject]@{}
    }
    if (-not $PrefsJson) { $PrefsJson = [PSCustomObject]@{} }
    if (-not ($PrefsJson.PSObject.Properties['bookmark_bar'])) {
        $PrefsJson | Add-Member -NotePropertyName 'bookmark_bar' -NotePropertyValue ([PSCustomObject]@{})
    }
    if (-not ($PrefsJson.bookmark_bar.PSObject.Properties['show_on_all_tabs'])) {
        $PrefsJson.bookmark_bar | Add-Member -NotePropertyName 'show_on_all_tabs' -NotePropertyValue $true
    } else {
        $PrefsJson.bookmark_bar.show_on_all_tabs = $true
    }
    $outputPrefs = $PrefsJson | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($PreferencesFile, $outputPrefs, [System.Text.Encoding]::UTF8)
}

Write-Host "Done! Launching Chrome..." -ForegroundColor Green
$chromeExe = "chrome.exe"
$commonChrome = "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe"
$commonChromeX86 = "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
if (Test-Path $commonChrome) { $chromeExe = $commonChrome }
elseif (Test-Path $commonChromeX86) { $chromeExe = $commonChromeX86 }
Start-Process $chromeExe -ErrorAction SilentlyContinue

