param(
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ((Split-Path -Leaf $root) -ieq "clamav") {
    $clamRoot = $root
} else {
    $clamRoot = Join-Path $root "tools\clamav"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("k3-clamav-" + [Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot "clamav.zip"

New-Item -ItemType Directory -Force -Path $clamRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $clamRoot "database") | Out-Null
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

try {
    if ($Version -eq "latest") {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/Cisco-Talos/clamav/releases/latest" -Headers @{ "User-Agent" = "K3-USB-App" }
        $asset = $release.assets | Where-Object { $_.name -match "win-x64.*\.zip$" -or $_.name -match "win64.*\.zip$" } | Select-Object -First 1
        if (-not $asset) {
            $asset = $release.assets | Where-Object { $_.name -match "windows.*\.zip$" -or $_.name -match "\.zip$" } | Select-Object -First 1
        }
        if (-not $asset) { throw "Khong tim thay file ZIP Windows trong release moi nhat cua ClamAV." }
        $url = $asset.browser_download_url
    } else {
        $url = $Version
    }

    Write-Host "Downloading ClamAV: $url"
    Invoke-WebRequest -Uri $url -OutFile $zipPath -Headers @{ "User-Agent" = "K3-USB-App" }

    Write-Host "Extracting..."
    Expand-Archive -LiteralPath $zipPath -DestinationPath $tempRoot -Force

    $clamscan = Get-ChildItem -Path $tempRoot -Recurse -Filter clamscan.exe | Select-Object -First 1
    if (-not $clamscan) { throw "Khong tim thay clamscan.exe trong goi vua tai." }

    $sourceDir = $clamscan.Directory.FullName
    Copy-Item -Path (Join-Path $sourceDir "*") -Destination $clamRoot -Recurse -Force

    $conf = @"
DatabaseDirectory database
DatabaseMirror database.clamav.net
UpdateLogFile freshclam.log
LogTime yes
"@
    Set-Content -LiteralPath (Join-Path $clamRoot "freshclam.conf") -Value $conf -Encoding UTF8

    Write-Host "Installed ClamAV portable to: $clamRoot"
    Write-Host "Run freshclam now? This needs internet."
    $freshclam = Join-Path $clamRoot "freshclam.exe"
    $freshclamConfig = Join-Path $clamRoot "freshclam.conf"
    & $freshclam "--config-file=$freshclamConfig" --stdout
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
