param(
    [string]$UsbPath = "",
    [string]$DistPath = "",
    [switch]$SkipBuild,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ([string]::IsNullOrWhiteSpace($DistPath)) {
    $DistPath = Join-Path $root "dist\USB-An-Toan-K3-Windows"
}

function Write-Info($message) {
    Write-Host "==> $message"
}

function Ensure-Dir($path) {
    if (-not (Test-Path -LiteralPath $path)) {
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }
}

function Copy-IfExists($source, $dest) {
    if (Test-Path -LiteralPath $source) {
        Ensure-Dir (Split-Path -Parent $dest)
        Copy-Item -LiteralPath $source -Destination $dest -Force
    }
}

function Hide-IfExists($path) {
    if (Test-Path -LiteralPath $path) {
        try {
            $item = Get-Item -LiteralPath $path -Force
            $item.Attributes = $item.Attributes -bor [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System
        } catch {
            Write-Warning "Cannot hide $path`: $($_.Exception.Message)"
        }
    }
}

function Get-RelativeHashEntry($target, $relativePath) {
    $file = Join-Path $target $relativePath
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { return $null }
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    $size = (Get-Item -LiteralPath $file).Length
    [ordered]@{
        path = $relativePath.Replace("\", "/")
        sha256 = $hash
        size = $size
    }
}

function Write-IntegrityManifest($target) {
    $tracked = @(
        "AnToanUSB.exe",
        "K3 Mac.app\Contents\MacOS\K3UsbSafeMac",
        "tools\rules\k3-rules.json"
    )
    $files = @()
    foreach ($relative in $tracked) {
        $entry = Get-RelativeHashEntry $target $relative
        if ($null -ne $entry) { $files += [pscustomobject]$entry }
    }

    $manifest = [ordered]@{
        version = 1
        generated_at = (Get-Date).ToUniversalTime().ToString("o")
        files = $files
    }
    $path = Join-Path $target ".k3_integrity_manifest.json"
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding UTF8
    Hide-IfExists $path
}

function Write-ReleaseReport($target) {
    $report = Join-Path $target "K3_RELEASE_REPORT_WINDOWS.txt"
    $exe = Join-Path $target "AnToanUSB.exe"
    $launcher = Join-Path $target "AutoLauncher\K3AutoLauncher.exe"
    $rules = Join-Path $target "tools\rules\k3-rules.json"
    $clamScan = Join-Path $target "tools\clamav\clamscan.exe"
    $freshClam = Join-Path $target "tools\clamav\freshclam.exe"
    $clamDb = Join-Path $target "tools\clamav\database\main.cvd"

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("USB An Toan K3 - Windows Release Report")
    $lines.Add("Generated: $((Get-Date).ToUniversalTime().ToString("o"))")
    $lines.Add("Target: $target")
    $lines.Add("")
    $lines.Add("[Windows]")
    if (Test-Path -LiteralPath $exe -PathType Leaf) {
        $lines.Add("AnToanUSB.exe: OK ($((Get-Item -LiteralPath $exe).Length) bytes)")
        $lines.Add("AnToanUSB SHA-256: $((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant())")
    } else {
        $lines.Add("AnToanUSB.exe: MISSING")
    }
    if (Test-Path -LiteralPath $launcher -PathType Leaf) {
        $lines.Add("K3AutoLauncher.exe: OK ($((Get-Item -LiteralPath $launcher).Length) bytes)")
    } else {
        $lines.Add("K3AutoLauncher.exe: MISSING")
    }
    $lines.Add("")
    $lines.Add("[Rules]")
    if (Test-Path -LiteralPath $rules -PathType Leaf) {
        $lines.Add("k3-rules.json: OK ($((Get-Item -LiteralPath $rules).Length) bytes)")
    } else {
        $lines.Add("k3-rules.json: MISSING")
    }
    $lines.Add("")
    $lines.Add("[ClamAV portable]")
    $lines.Add($(if (Test-Path -LiteralPath $clamScan -PathType Leaf) { "clamscan.exe: OK" } else { "clamscan.exe: MISSING" }))
    $lines.Add($(if (Test-Path -LiteralPath $freshClam -PathType Leaf) { "freshclam.exe: OK" } else { "freshclam.exe: MISSING" }))
    $lines.Add($(if (Test-Path -LiteralPath $clamDb -PathType Leaf) { "database/main.cvd: OK ($((Get-Item -LiteralPath $clamDb).Length) bytes)" } else { "database/main.cvd: MISSING" }))
    $lines.Add("")
    $lines.Add("[Integrity]")
    $lines.Add($(if (Test-Path -LiteralPath (Join-Path $target ".k3_integrity_manifest.json") -PathType Leaf) { ".k3_integrity_manifest.json: OK" } else { ".k3_integrity_manifest.json: MISSING" }))
    $lines.Add("")
    $lines.Add("[Data safety]")
    $lines.Add("This script never deletes .vault, .vault_decoy, .vault_config.json, quarantine, trusted hashes, BaoMat, or ClamAV database from a USB target.")

    $lines | Set-Content -LiteralPath $report -Encoding UTF8
}

function Prepare-ReleaseLayout($target, [bool]$CleanTarget) {
    if ($CleanTarget -and (Test-Path -LiteralPath $target)) {
        $resolvedRoot = [System.IO.Path]::GetFullPath($root)
        $resolvedTarget = [System.IO.Path]::GetFullPath($target)
        if (-not $resolvedTarget.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean target outside repo: $resolvedTarget"
        }
        Remove-Item -LiteralPath $target -Recurse -Force
    }

    Ensure-Dir $target
    Ensure-Dir (Join-Path $target "AutoLauncher")
    Ensure-Dir (Join-Path $target "tools\rules")
    Ensure-Dir (Join-Path $target "tools\clamav\database")
    Ensure-Dir (Join-Path $target ".vault")
    Ensure-Dir (Join-Path $target ".vault_decoy")
    Ensure-Dir (Join-Path $target "BaoMat")

    Copy-IfExists (Join-Path $root "src-csharp\AnToanUSB.exe") (Join-Path $target "AnToanUSB.exe")
    Copy-IfExists (Join-Path $root "src-csharp\K3AutoLauncher.exe") (Join-Path $target "AutoLauncher\K3AutoLauncher.exe")
    Copy-IfExists (Join-Path $root "src-csharp\CaiDat-AutoLauncher.bat") (Join-Path $target "AutoLauncher\CaiDat-AutoLauncher.bat")
    Copy-IfExists (Join-Path $root "src-csharp\Go-AutoLauncher.bat") (Join-Path $target "AutoLauncher\Go-AutoLauncher.bat")
    Copy-IfExists (Join-Path $root "src-csharp\autorun.inf") (Join-Path $target "autorun.inf")
    Copy-IfExists (Join-Path $root "public\icon.png") (Join-Path $target "icon.png")
    Copy-IfExists (Join-Path $root "tools\rules\k3-rules.json") (Join-Path $target "tools\rules\k3-rules.json")
    Copy-IfExists (Join-Path $root "src-csharp\Install-ClamAV-Portable.ps1") (Join-Path $target "tools\clamav\Install-ClamAV-Portable.ps1")
    Copy-IfExists (Join-Path $root "src-csharp\CaiDat-ClamAV-Portable.bat") (Join-Path $target "tools\clamav\CaiDat-ClamAV-Portable.bat")

    $readme = @"
USB An Toan K3 - Windows

1. Double click AnToanUSB.exe.
2. First run creates .vault, .vault_decoy, .vault_config.json and BaoMat.
3. Do not delete hidden/system folders. They contain vault/config/quarantine data.
4. ClamAV portable can be installed later from tools\clamav\CaiDat-ClamAV-Portable.bat.

Data safety: updating this package must not delete .vault, .vault_decoy, .vault_config.json, .k3_quarantine, .k3_trusted_hashes.txt or BaoMat.
"@
    Set-Content -LiteralPath (Join-Path $target "README-WINDOWS-K3.txt") -Value $readme -Encoding UTF8

    foreach ($hidden in @(".vault", ".vault_decoy", ".k3_integrity_manifest.json", ".k3_trusted_hashes.txt", "AutoLauncher", "tools", "autorun.inf", "icon.png")) {
        Hide-IfExists (Join-Path $target $hidden)
    }

    Write-IntegrityManifest $target
    Write-ReleaseReport $target
}

if (-not $SkipBuild) {
    Write-Info "Building Windows app"
    Push-Location (Join-Path $root "src-csharp")
    try {
        & ".\build.bat"
        if ($LASTEXITCODE -ne 0) { throw "build.bat failed with exit code $LASTEXITCODE" }
        & ".\build-launcher.bat"
        if ($LASTEXITCODE -ne 0) { throw "build-launcher.bat failed with exit code $LASTEXITCODE" }
    } finally {
        Pop-Location
    }
}

Write-Info "Preparing dist: $DistPath"
Prepare-ReleaseLayout $DistPath $true

if (-not $NoZip) {
    $zipPath = Join-Path $root "dist\USB-An-Toan-K3-Windows.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $DistPath "*") -DestinationPath $zipPath -Force
    Write-Info "Zip: $zipPath"
}

if (-not [string]::IsNullOrWhiteSpace($UsbPath)) {
    if (-not (Test-Path -LiteralPath $UsbPath -PathType Container)) {
        throw "USB target does not exist: $UsbPath"
    }
    Write-Info "Updating USB target without deleting vault data: $UsbPath"
    Prepare-ReleaseLayout $UsbPath $false
}

Write-Info "Done"
Write-Host "Dist: $DistPath"
if (-not [string]::IsNullOrWhiteSpace($UsbPath)) {
    Write-Host "USB:  $UsbPath"
}
