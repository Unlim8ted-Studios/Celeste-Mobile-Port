# BuildDeployMods.ps1
#
# Place this file directly inside:
#
#   Celeste-Mobile-Port\CelesteMobileMods\
#
# Repository structure:
#
#   AndroidWrapper\
#   IOSWrapper\
#   Celeste\
#   CelesteMobileMods\
#       BuildDeployMods.ps1
#       MobileBridge\
#       MobileTweaks\
#       MouseUI\
#       MobileMultiplayer\
#       BetterMapEditor\
#       AnyOtherMod\
#
# This script:
#   - automatically detects every immediate folder containing:
#       everest.yaml
#       one or more *.csproj files
#   - NEVER builds or modifies CelesteNet
#   - NEVER rewrites everest.yaml
#   - expects DLL: in everest.yaml to be a ROOT filename, e.g.
#       DLL: MobileBridge.dll
#   - builds every top-level .csproj in each detected mod folder
#   - packages:
#
#       everest.yaml
#       ModName.dll
#       Dialog\
#       Graphics\
#       Maps\
#       etc.
#
#   - NEVER places the packaged DLL in /bin
#   - only replaces installed ZIPs corresponding to detected workspace mods
#   - leaves CelesteNet, Everest, and unrelated installed mods alone
#
# Compatible with Windows PowerShell 5.1.

param(
    [string]$CelestePath = "D:\SteamLibrary\steamapps\common\Celeste"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$ModsRoot = $PSScriptRoot
$ModsDest = Join-Path $CelestePath "Mods"
$CelesteExe = Join-Path $CelestePath "Celeste.exe"

$ContentDirectories = @(
    "Ahorn",
    "Audio",
    "Content",
    "Dialog",
    "Graphics",
    "Loenn",
    "Maps",
    "Tutorials"
)

$ContentDirectoryAliases = @{
    "Dialogue" = "Dialog"
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Invoke-ModBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    Write-Host ""
    Write-Host ("Building {0}..." -f $ProjectPath) -ForegroundColor Cyan

    & dotnet build $ProjectPath -c Release --nologo -v minimal

    if ($LASTEXITCODE -ne 0) {
        throw ("dotnet build failed for: {0}" -f $ProjectPath)
    }
}


function Get-ManifestDllName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$YamlPath
    )

    $yamlText = Get-Content -LiteralPath $YamlPath -Raw

    # Accept:
    #
    #   DLL: MobileBridge.dll
    #   DLL: "MobileBridge.dll"
    #   DLL: 'MobileBridge.dll'
    #
    $match = [regex]::Match(
        $yamlText,
        '(?im)^\s*DLL\s*:\s*["'']?([^"''#\r\n]+?)["'']?\s*$'
    )

    if (-not $match.Success) {
        throw ("No DLL entry was found in {0}" -f $YamlPath)
    }

    $dllName = $match.Groups[1].Value.Trim()

    if ([string]::IsNullOrWhiteSpace($dllName)) {
        throw ("The DLL entry in {0} is empty." -f $YamlPath)
    }

    # We intentionally require the DLL to be at ZIP root.
    # DO NOT rewrite the YAML.
    if ($dllName.Contains("/") -or $dllName.Contains("\")) {
        throw (
            "The DLL entry must be a root-level filename. {0} currently contains DLL: {1}" -f `
                $YamlPath,
                $dllName
        )
    }

    $extension = [System.IO.Path]::GetExtension($dllName)

    if (-not [string]::Equals(
        $extension,
        ".dll",
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        throw (
            "The DLL entry in {0} is not a .dll filename: {1}" -f `
                $YamlPath,
                $dllName
        )
    }

    return $dllName
}


function Get-BuiltDll {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModPath,

        [Parameter(Mandatory = $true)]
        [string]$DllName
    )

    # Common output locations first.
    $preferredPaths = @(
        (Join-Path $ModPath ("bin\{0}" -f $DllName)),
        (Join-Path $ModPath ("bin\Release\{0}" -f $DllName)),
        (Join-Path $ModPath ("bin\Release\net8.0\{0}" -f $DllName)),
        (Join-Path $ModPath ("bin\Debug\{0}" -f $DllName)),
        (Join-Path $ModPath ("bin\Debug\net8.0\{0}" -f $DllName))
    )

    foreach ($candidate in $preferredPaths) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    # Fallback: search anywhere under the build output folder.
    $binRoot = Join-Path $ModPath "bin"

    if (Test-Path -LiteralPath $binRoot -PathType Container) {
        $matches = @(
            Get-ChildItem `
                -LiteralPath $binRoot `
                -Filter $DllName `
                -File `
                -Recurse `
                -ErrorAction SilentlyContinue |
            Where-Object {
                $_.FullName -notmatch '[\\/](ref|refint|obj)[\\/]'
            } |
            Sort-Object LastWriteTime -Descending
        )

        if ($matches.Count -gt 0) {
            return $matches[0].FullName
        }
    }

    throw (
        "Could not find built DLL {0} anywhere under {1}" -f `
            $DllName,
            $binRoot
    )
}


function New-ModZip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationZip
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (Test-Path -LiteralPath $DestinationZip) {
        Remove-Item -LiteralPath $DestinationZip -Force
    }

    $sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path

    $fileStream = New-Object System.IO.FileStream(
        $DestinationZip,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None
    )

    try {
        $archive = New-Object System.IO.Compression.ZipArchive(
            $fileStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false
        )

        try {
            $files = @(
                Get-ChildItem `
                    -LiteralPath $SourceDirectory `
                    -File `
                    -Recurse
            )

            foreach ($file in $files) {
                $relativePath = $file.FullName.Substring($sourceRoot.Length)

                $relativePath = $relativePath.TrimStart(
                    [char[]]@('\', '/')
                )

                # ZIP entry names should use forward slashes.
                $entryName = $relativePath.Replace('\', '/')

                Write-Host ("  ZIP: {0}" -f $entryName) -ForegroundColor DarkGray

                $entry = $archive.CreateEntry(
                    $entryName,
                    [System.IO.Compression.CompressionLevel]::Optimal
                )

                $inputStream = $null
                $outputStream = $null

                try {
                    $inputStream = [System.IO.File]::OpenRead($file.FullName)
                    $outputStream = $entry.Open()

                    $inputStream.CopyTo($outputStream)
                }
                finally {
                    if ($null -ne $outputStream) {
                        $outputStream.Dispose()
                    }

                    if ($null -ne $inputStream) {
                        $inputStream.Dispose()
                    }
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $fileStream.Dispose()
    }
}


function Test-ModZip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ZipPath,

        [Parameter(Mandatory = $true)]
        [string]$DllName
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)

    try {
        $entryNames = @(
            $archive.Entries |
            ForEach-Object {
                $_.FullName
            }
        )

        Write-Host ""
        Write-Host ("Archive contents for {0}:" -f $ZipPath) -ForegroundColor DarkCyan

        foreach ($entryName in $entryNames) {
            Write-Host ("  [{0}]" -f $entryName)
        }

        if ($entryNames -notcontains "everest.yaml") {
            throw (
                "Archive is missing everest.yaml at ZIP root: {0}" -f `
                    $ZipPath
            )
        }

        if ($entryNames -notcontains $DllName) {
            throw (
                "Archive is missing root DLL {0}: {1}" -f `
                    $DllName,
                    $ZipPath
            )
        }

        # Absolutely reject packaged DLLs under bin/.
        $badDllEntries = @(
            $entryNames |
            Where-Object {
                $_ -match '(^|/)bin/.*\.dll$'
            }
        )

        if ($badDllEntries.Count -gt 0) {
            throw (
                "Archive incorrectly contains DLL files under bin/: {0}" -f `
                    ($badDllEntries -join ", ")
            )
        }

        # Also reject Windows-style backslashes in ZIP entry names.
        $badSlashEntries = @(
            $entryNames |
            Where-Object {
                $_.Contains("\")
            }
        )

        if ($badSlashEntries.Count -gt 0) {
            throw (
                "Archive contains invalid backslash ZIP paths: {0}" -f `
                    ($badSlashEntries -join ", ")
            )
        }

        Write-Host (
            "Verified: everest.yaml and {0} are both at ZIP root." -f `
                $DllName
        ) -ForegroundColor Green
    }
    finally {
        $archive.Dispose()
    }
}


# ---------------------------------------------------------------------------
# Validate environment
# ---------------------------------------------------------------------------

if (-not (Test-Path -LiteralPath $CelestePath -PathType Container)) {
    throw (
        "Celeste directory does not exist: {0}" -f `
            $CelestePath
    )
}

if (-not (Test-Path -LiteralPath $CelesteExe -PathType Leaf)) {
    throw (
        "Celeste.exe was not found: {0}" -f `
            $CelesteExe
    )
}

if (-not (Test-Path -LiteralPath $ModsRoot -PathType Container)) {
    throw (
        "CelesteMobileMods directory does not exist: {0}" -f `
            $ModsRoot
    )
}

if (-not (Test-Path -LiteralPath $ModsDest -PathType Container)) {
    New-Item `
        -ItemType Directory `
        -Path $ModsDest `
        -Force |
    Out-Null
}

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found in PATH."
}


# ---------------------------------------------------------------------------
# Detect source mods
# ---------------------------------------------------------------------------

$DetectedMods = @(
    Get-ChildItem `
        -LiteralPath $ModsRoot `
        -Directory |
    Where-Object {
        $directory = $_

        $yamlPath = Join-Path $directory.FullName "everest.yaml"

        $projects = @(
            Get-ChildItem `
                -LiteralPath $directory.FullName `
                -File `
                -Filter "*.csproj" `
                -ErrorAction SilentlyContinue
        )

        $isCelesteNet =
            $directory.Name -match '^CelesteNet($|\.)'

        (-not $isCelesteNet) -and
        (Test-Path -LiteralPath $yamlPath -PathType Leaf) -and
        ($projects.Count -gt 0)
    } |
    Sort-Object Name
)

if ($DetectedMods.Count -eq 0) {
    throw (
        "No mod folders containing both everest.yaml and a top-level .csproj were found in {0}" -f `
            $ModsRoot
    )
}

Write-Host ""
Write-Host (
    "Detected {0} source mod(s):" -f `
        $DetectedMods.Count
) -ForegroundColor Green

foreach ($mod in $DetectedMods) {
    Write-Host ("  - {0}" -f $mod.Name)
}

Write-Host ""
Write-Host "CelesteNet is external and will NOT be built, packaged, deleted, or deployed." -ForegroundColor Yellow


# ---------------------------------------------------------------------------
# Stop Celeste
# ---------------------------------------------------------------------------

$runningCeleste = @(
    Get-Process `
        -Name "Celeste" `
        -ErrorAction SilentlyContinue
)

if ($runningCeleste.Count -gt 0) {
    Write-Host ""
    Write-Host "Closing Celeste..." -ForegroundColor Yellow

    $runningCeleste |
    Stop-Process -Force

    $tries = 0

    while ($tries -lt 30) {
        Start-Sleep -Milliseconds 100

        $stillRunning = @(
            Get-Process `
                -Name "Celeste" `
                -ErrorAction SilentlyContinue
        )

        if ($stillRunning.Count -eq 0) {
            break
        }

        $tries++
    }

    $stillRunning = @(
        Get-Process `
            -Name "Celeste" `
            -ErrorAction SilentlyContinue
    )

    if ($stillRunning.Count -gt 0) {
        throw "Celeste is still running."
    }
}


# ---------------------------------------------------------------------------
# Build/package/deploy
# ---------------------------------------------------------------------------

$Failures = New-Object System.Collections.Generic.List[string]


foreach ($mod in $DetectedMods) {
    $ModName = $mod.Name
    $ModPath = $mod.FullName

    Write-Host ""
    Write-Host ("=== Processing {0} ===" -f $ModName) -ForegroundColor Magenta

    try {
        $YamlSource = Join-Path $ModPath "everest.yaml"
        $DllName = Get-ManifestDllName -YamlPath $YamlSource

        Write-Host ("Manifest DLL: {0}" -f $DllName) -ForegroundColor DarkGray

        $Projects = @(
            Get-ChildItem `
                -LiteralPath $ModPath `
                -File `
                -Filter "*.csproj" |
            Sort-Object Name
        )

        if ($Projects.Count -eq 0) {
            throw (
                "No top-level .csproj files found in {0}" -f `
                    $ModPath
            )
        }

        foreach ($project in $Projects) {
            Invoke-ModBuild -ProjectPath $project.FullName
        }

        $BuiltDll = Get-BuiltDll `
            -ModPath $ModPath `
            -DllName $DllName

        Write-Host (
            "Built DLL found at: {0}" -f `
                $BuiltDll
        ) -ForegroundColor DarkGray


        # Use Windows TEMP, not a folder inside the source mod.
        # This prevents stale temp_dist/bin folders from being packaged.
        $TempName = "celeste-mobile-mod-" + [Guid]::NewGuid().ToString("N")

        $StagePath = Join-Path `
            ([System.IO.Path]::GetTempPath()) `
            $TempName

        New-Item `
            -ItemType Directory `
            -Path $StagePath `
            -Force |
        Out-Null

        try {
            # ---------------------------------------------------------------
            # everest.yaml
            #
            # COPY EXACTLY.
            # DO NOT MODIFY.
            # ---------------------------------------------------------------

            Copy-Item `
                -LiteralPath $YamlSource `
                -Destination (Join-Path $StagePath "everest.yaml") `
                -Force


            # ---------------------------------------------------------------
            # DLL
            #
            # COPY DIRECTLY TO STAGING ROOT.
            #
            # NO:
            #   StagePath\bin\
            #
            # YES:
            #   StagePath\MobileBridge.dll
            # ---------------------------------------------------------------

            $RootDllDestination = Join-Path $StagePath $DllName

            Copy-Item `
                -LiteralPath $BuiltDll `
                -Destination $RootDllDestination `
                -Force


            if (-not (Test-Path -LiteralPath $RootDllDestination -PathType Leaf)) {
                throw (
                    "DLL was not copied to staging root: {0}" -f `
                        $RootDllDestination
                )
            }


            # ---------------------------------------------------------------
            # Everest content folders
            # ---------------------------------------------------------------

            foreach ($contentDirectory in $ContentDirectories) {
                $sourceContent = Join-Path $ModPath $contentDirectory

                if (Test-Path -LiteralPath $sourceContent -PathType Container) {
                    $destinationContent = Join-Path $StagePath $contentDirectory

                    Copy-Item `
                        -LiteralPath $sourceContent `
                        -Destination $destinationContent `
                        -Recurse `
                        -Force
                }
            }

            foreach ($alias in $ContentDirectoryAliases.GetEnumerator()) {
                $sourceContent = Join-Path $ModPath $alias.Key

                if (Test-Path -LiteralPath $sourceContent -PathType Container) {
                    $destinationContent = Join-Path $StagePath $alias.Value

                    Copy-Item `
                        -LiteralPath $sourceContent `
                        -Destination $destinationContent `
                        -Recurse `
                        -Force
                }
            }


            # ---------------------------------------------------------------
            # Package
            # ---------------------------------------------------------------

            $ZipPath = Join-Path $ModPath ($ModName + ".zip")

            Write-Host ""
            Write-Host (
                "Packaging {0}..." -f `
                    $ZipPath
            ) -ForegroundColor Cyan

            New-ModZip `
                -SourceDirectory $StagePath `
                -DestinationZip $ZipPath


            # ---------------------------------------------------------------
            # Verify exact package structure
            # ---------------------------------------------------------------

            Test-ModZip `
                -ZipPath $ZipPath `
                -DllName $DllName


            # ---------------------------------------------------------------
            # Deploy
            #
            # Only replace THIS workspace mod.
            #
            # CelesteNet and unrelated installed mods are never touched.
            # ---------------------------------------------------------------

            $DeployPath = Join-Path $ModsDest ($ModName + ".zip")

            if (Test-Path -LiteralPath $DeployPath -PathType Leaf) {
                Remove-Item `
                    -LiteralPath $DeployPath `
                    -Force
            }

            Write-Host ""
            Write-Host (
                "Deploying to {0}" -f `
                    $DeployPath
            ) -ForegroundColor Yellow

            Copy-Item `
                -LiteralPath $ZipPath `
                -Destination $DeployPath `
                -Force
        }
        finally {
            if (Test-Path -LiteralPath $StagePath) {
                Remove-Item `
                    -LiteralPath $StagePath `
                    -Recurse `
                    -Force `
                    -ErrorAction SilentlyContinue
            }
        }

        Write-Host ""
        Write-Host (
            "{0} completed successfully." -f `
                $ModName
        ) -ForegroundColor Green
    }
    catch {
        $failureMessage = (
            "{0} failed: {1}" -f `
                $ModName,
                $_.Exception.Message
        )

        $Failures.Add($failureMessage)

        Write-Host ""
        Write-Host $failureMessage -ForegroundColor Red
    }
}


# ---------------------------------------------------------------------------
# Finish
# ---------------------------------------------------------------------------

if ($Failures.Count -gt 0) {
    Write-Host ""
    Write-Host (
        "Build/deploy completed with {0} failure(s):" -f `
            $Failures.Count
    ) -ForegroundColor Red

    foreach ($failure in $Failures) {
        Write-Host ("  - {0}" -f $failure) -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Celeste was NOT restarted because at least one mod failed." -ForegroundColor Yellow

    exit 1
}


Write-Host ""
Write-Host "All detected mods built, packaged, and deployed successfully." -ForegroundColor Green
Write-Host "CelesteNet was left untouched." -ForegroundColor Green

Write-Host ""
Write-Host "Restarting Celeste..." -ForegroundColor Yellow

Start-Process `
    -FilePath $CelesteExe `
    -WorkingDirectory $CelestePath
