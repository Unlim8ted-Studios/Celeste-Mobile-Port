# Build, package, and deploy every Everest code mod in CelesteMobileMods.
#
# CelesteNet is an EXTERNAL dependency:
#   - this script does not build it
#   - this script does not package it
#   - this script does not delete it
#   - this script does not deploy it
#
# Expected repository structure:
#
#   AndroidWrapper/
#   IOSWrapper/
#   Celeste/
#   CelesteMobileMods/
#       MobileBridge/
#       MobileTweaks/
#       MouseUI/
#       MobileMultiplayer/
#       BetterMapEditor/
#       AnyOtherCodeMod/
#
# A folder is auto-detected as a buildable mod when it contains BOTH:
#   everest.yaml
#   one or more top-level *.csproj files
#
# Each detected mod's own everest.yaml is trusted exactly as written.
# The DLL: entry MUST name a root-level DLL, for example:
#
#   DLL: MobileBridge.dll
#
# The packaged ZIP therefore contains:
#
#   everest.yaml
#   MobileBridge.dll
#   Dialogue/...
#   Graphics/...
#   etc.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$CelestePath = "D:\SteamLibrary\steamapps\common\Celeste"
$ModsDest = Join-Path $CelestePath "Mods"

# The script can live either:
#   1. at repository root, beside CelesteMobileMods/
#   2. inside CelesteMobileMods/
$RepoOrModsRoot = $PSScriptRoot
$NestedModsRoot = Join-Path $RepoOrModsRoot "CelesteMobileMods"

if (Test-Path -LiteralPath $NestedModsRoot -PathType Container) {
    $RepoRoot = $RepoOrModsRoot
    $ModsRoot = $NestedModsRoot
} else {
    $RepoRoot = Split-Path -Parent $RepoOrModsRoot
    $ModsRoot = $RepoOrModsRoot
}

$RuntimeModsDest = Join-Path $RepoRoot "CelesteRuntime\Mods"
$CecilPath = Join-Path $RepoRoot "tools\WasmMmPatch\Mono.Cecil.dll"
$FrameworkRoot = Join-Path $RepoRoot "CelesteRuntime\_framework"

$ContentDirectories = @(
    "Ahorn",
    "Audio",
    "Content",
    "Dialog",
    "Dialogue",
    "Graphics",
    "Loenn",
    "Maps",
    "Tutorials"
)

function Invoke-DotNetBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    Write-Host "Building $ProjectPath..." -ForegroundColor Cyan

    & dotnet build $ProjectPath -c Release --nologo -v minimal

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for: $ProjectPath"
    }
}

function Ensure-CecilLoaded {
    if (!([System.Management.Automation.PSTypeName]"Mono.Cecil.ModuleDefinition").Type) {
        if (!(Test-Path -LiteralPath $CecilPath -PathType Leaf)) {
            throw "Mono.Cecil was not found for assembly normalization: $CecilPath"
        }

        [System.Reflection.Assembly]::LoadFrom($CecilPath) | Out-Null
    }
}

function Get-FrameworkAssemblyIdentityMap {
    Ensure-CecilLoaded

    $map = @{}

    if (!(Test-Path -LiteralPath $FrameworkRoot -PathType Container)) {
        throw "Framework directory was not found: $FrameworkRoot"
    }

    foreach ($dll in Get-ChildItem -LiteralPath $FrameworkRoot -Filter "*.dll" -File) {
        $module = $null

        try {
            $module = [Mono.Cecil.ModuleDefinition]::ReadModule($dll.FullName)
            $name = $module.Assembly.Name
            $map[$name.Name] = @{
                Version = $name.Version
                PublicKeyToken = $name.PublicKeyToken
            }
        }
        finally {
            if ($module -ne $null) {
                $module.Dispose()
            }
        }
    }

    return $map
}

function Normalize-ModAssemblyReferences {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DllPath,

        [Parameter(Mandatory = $true)]
        [hashtable]$FrameworkAssemblies
    )

    Ensure-CecilLoaded

    $readerParameters = [Mono.Cecil.ReaderParameters]::new()
    $readerParameters.InMemory = $true

    $module = [Mono.Cecil.ModuleDefinition]::ReadModule(
        $DllPath,
        $readerParameters)

    $changed = $false

    try {
        foreach ($reference in $module.AssemblyReferences) {
            if (!$FrameworkAssemblies.ContainsKey($reference.Name)) {
                continue
            }

            $identity = $FrameworkAssemblies[$reference.Name]

            if ($reference.Version -ne $identity.Version) {
                $reference.Version = $identity.Version
                $changed = $true
            }

            if ($identity.PublicKeyToken -and $identity.PublicKeyToken.Length -gt 0) {
                $reference.PublicKeyToken = $identity.PublicKeyToken
            }
        }

        if (!$changed) {
            return
        }

        $tmp = "$DllPath.tmp"

        if (Test-Path -LiteralPath $tmp -PathType Leaf) {
            Remove-Item -LiteralPath $tmp -Force
        }

        $module.Write($tmp)
        Move-Item -LiteralPath $tmp -Destination $DllPath -Force

        Write-Host "Normalized framework references in $DllPath" -ForegroundColor DarkGreen
    }
    finally {
        $module.Dispose()
    }
}

function Get-ManifestDllName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$YamlPath
    )

    $yaml = Get-Content -LiteralPath $YamlPath -Raw

    $match = [regex]::Match(
        $yaml,
        '(?im)^\s*DLL\s*:\s*["'']?([^"''#\r\n]+?)["'']?\s*$'
    )

    if (!$match.Success) {
        throw "No DLL: entry was found in $YamlPath"
    }

    $dll = $match.Groups[1].Value.Trim()

    # The YAML is NOT rewritten. Instead, fail loudly if it doesn't describe
    # the flat package layout this repository uses.
    if ($dll.Contains("/") -or $dll.Contains("\")) {
        throw "DLL must be at the ZIP root. '$YamlPath' currently says: DLL: $dll"
    }

    if (![string]::Equals(
        [System.IO.Path]::GetExtension($dll),
        ".dll",
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        throw "DLL entry is not a .dll filename in ${YamlPath}: $dll"
    }

    return $dll
}

function Get-BuiltDll {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModPath,

        [Parameter(Mandatory = $true)]
        [string]$DllName
    )

    $preferred = @(
        (Join-Path $ModPath "bin\$DllName"),
        (Join-Path $ModPath "bin\Release\net8.0\$DllName"),
        (Join-Path $ModPath "bin\Release\$DllName")
    )

    foreach ($candidate in $preferred) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

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

    throw "Could not find built DLL '$DllName' below $ModPath\bin"
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

    $stream = [System.IO.File]::Open(
        $DestinationZip,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None
    )

    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false
        )

        try {
            $basePath = (Resolve-Path -LiteralPath $SourceDirectory).Path

            foreach ($file in Get-ChildItem -LiteralPath $SourceDirectory -File -Recurse) {
                $relative = $file.FullName.Substring($basePath.Length).TrimStart('\', '/')
                $entryName = $relative.Replace('\', '/')

                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive,
                    $file.FullName,
                    $entryName,
                    [System.IO.Compression.CompressionLevel]::Optimal
                ) | Out-Null
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Assert-ModZip {
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
        $entries = @($archive.Entries | ForEach-Object { $_.FullName })

        if ($entries -notcontains "everest.yaml") {
            throw "Archive is missing root everest.yaml: $ZipPath"
        }

        if ($entries -notcontains $DllName) {
            throw "Archive is missing root DLL '$DllName': $ZipPath"
        }

        $nestedDlls = @(
            $entries | Where-Object {
                $_ -match '(^|/)bin/.*\.dll$'
            }
        )

        if ($nestedDlls.Count -gt 0) {
            throw "Archive incorrectly contains DLL(s) in /bin: $($nestedDlls -join ', ')"
        }

        Write-Host "Verified ZIP root: everest.yaml + $DllName" -ForegroundColor DarkGreen
    }
    finally {
        $archive.Dispose()
    }
}

if (!(Test-Path -LiteralPath $CelestePath -PathType Container)) {
    throw "Celeste directory does not exist: $CelestePath"
}

$CelesteExe = Join-Path $CelestePath "Celeste.exe"

if (!(Test-Path -LiteralPath $CelesteExe -PathType Leaf)) {
    throw "Celeste.exe was not found: $CelesteExe"
}

if (!(Test-Path -LiteralPath $ModsDest -PathType Container)) {
    New-Item -ItemType Directory -Path $ModsDest -Force | Out-Null
}

if (!(Test-Path -LiteralPath $RuntimeModsDest -PathType Container)) {
    New-Item -ItemType Directory -Path $RuntimeModsDest -Force | Out-Null
}

if (!(Test-Path -LiteralPath $ModsRoot -PathType Container)) {
    throw "CelesteMobileMods directory was not found: $ModsRoot"
}

if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found in PATH."
}

# Auto-detect every immediate code-mod folder. No mod-name list is necessary.
#
# CelesteNet is explicitly excluded even if a source checkout is ever placed
# beside these mods, because it is managed externally.
$DetectedMods = @(
    Get-ChildItem -LiteralPath $ModsRoot -Directory |
    Where-Object {
        $_.Name -notmatch '^CelesteNet($|\.)' -and
        (Test-Path -LiteralPath (Join-Path $_.FullName "everest.yaml") -PathType Leaf) -and
        @(
            Get-ChildItem `
                -LiteralPath $_.FullName `
                -File `
                -Filter "*.csproj" `
                -ErrorAction SilentlyContinue
        ).Count -gt 0
    } |
    Sort-Object Name
)

if ($DetectedMods.Count -eq 0) {
    throw "No folders with both everest.yaml and a top-level .csproj were found in $ModsRoot"
}

Write-Host "Detected $($DetectedMods.Count) source mod(s):" -ForegroundColor Green

foreach ($mod in $DetectedMods) {
    Write-Host "  - $($mod.Name)"
}

Write-Host "CelesteNet is external and will NOT be built, removed, packaged, or deployed." -ForegroundColor DarkGray

$FrameworkAssemblies = Get-FrameworkAssemblyIdentityMap

# Close Celeste first.
Write-Host "Checking for running Celeste process..." -ForegroundColor Yellow

$CelesteProc = Get-Process -Name "Celeste" -ErrorAction SilentlyContinue

if ($CelesteProc) {
    Write-Host "Closing Celeste..." -ForegroundColor Yellow
    $CelesteProc | Stop-Process -Force

    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 100

        if (!(Get-Process -Name "Celeste" -ErrorAction SilentlyContinue)) {
            break
        }
    }

    if (Get-Process -Name "Celeste" -ErrorAction SilentlyContinue) {
        throw "Celeste is still running after Stop-Process."
    }
}

# Remove only ZIPs corresponding to source mods that this script is rebuilding.
# CelesteNet and unrelated installed mods are left completely untouched.
Write-Host "Cleaning previously deployed workspace mod ZIPs..." -ForegroundColor Yellow

foreach ($mod in $DetectedMods) {
    $deployedZip = Join-Path $ModsDest "$($mod.Name).zip"
    $runtimeZip = Join-Path $RuntimeModsDest "$($mod.Name).zip"

    if (Test-Path -LiteralPath $deployedZip -PathType Leaf) {
        Remove-Item -LiteralPath $deployedZip -Force
    }

    if (Test-Path -LiteralPath $runtimeZip -PathType Leaf) {
        Remove-Item -LiteralPath $runtimeZip -Force
    }
}

$Failures = [System.Collections.Generic.List[string]]::new()

foreach ($mod in $DetectedMods) {
    $ModName = $mod.Name
    $ModPath = $mod.FullName

    Write-Host "`n=== Processing $ModName ===" -ForegroundColor Magenta

    try {
        $YamlSource = Join-Path $ModPath "everest.yaml"
        $DllName = Get-ManifestDllName -YamlPath $YamlSource

        # Build every top-level project in the folder.
        # This intentionally does not assume a project naming convention.
        $Projects = @(
            Get-ChildItem `
                -LiteralPath $ModPath `
                -File `
                -Filter "*.csproj" |
            Sort-Object Name
        )

        foreach ($project in $Projects) {
            Invoke-DotNetBuild -ProjectPath $project.FullName
        }

        $DllSource = Get-BuiltDll -ModPath $ModPath -DllName $DllName
        Normalize-ModAssemblyReferences `
            -DllPath $DllSource `
            -FrameworkAssemblies $FrameworkAssemblies

        Write-Host "Using built DLL: $DllSource" -ForegroundColor DarkGray
        Write-Host "Manifest DLL: $DllName" -ForegroundColor DarkGray

        # Stage outside the project tree so a stale temp_dist/bin can never
        # accidentally become part of the archive.
        $TempPath = Join-Path `
            ([System.IO.Path]::GetTempPath()) `
            ("celeste-mobile-mod-" + [guid]::NewGuid().ToString("N"))

        New-Item -ItemType Directory -Path $TempPath -Force | Out-Null

        try {
            # Trust/copy the source YAML exactly as written.
            Copy-Item `
                -LiteralPath $YamlSource `
                -Destination (Join-Path $TempPath "everest.yaml") `
                -Force

            # Root-level DLL. NO package bin/ directory.
            Copy-Item `
                -LiteralPath $DllSource `
                -Destination (Join-Path $TempPath $DllName) `
                -Force

            # Include standard Everest content folders when present.
            foreach ($contentDirectory in $ContentDirectories) {
                $sourceContent = Join-Path $ModPath $contentDirectory

                if (Test-Path -LiteralPath $sourceContent -PathType Container) {
                    Copy-Item `
                        -LiteralPath $sourceContent `
                        -Destination $TempPath `
                        -Recurse `
                        -Force
                }
            }

            $ZipPath = Join-Path $ModPath "$ModName.zip"

            New-ModZip `
                -SourceDirectory $TempPath `
                -DestinationZip $ZipPath

            Assert-ModZip `
                -ZipPath $ZipPath `
                -DllName $DllName

            $DeployPath = Join-Path $ModsDest "$ModName.zip"

            Write-Host "Deploying $ModName.zip to $DeployPath..." -ForegroundColor Yellow

            Copy-Item `
                -LiteralPath $ZipPath `
                -Destination $DeployPath `
                -Force

            $RuntimeDeployPath = Join-Path $RuntimeModsDest "$ModName.zip"

            Write-Host "Bundling $ModName.zip to $RuntimeDeployPath..." -ForegroundColor Yellow

            Copy-Item `
                -LiteralPath $ZipPath `
                -Destination $RuntimeDeployPath `
                -Force
        }
        finally {
            if (Test-Path -LiteralPath $TempPath) {
                Remove-Item -LiteralPath $TempPath -Recurse -Force
            }
        }

        Write-Host "$ModName completed successfully." -ForegroundColor Green
    }
    catch {
        $message = "$ModName failed: $($_.Exception.Message)"
        $Failures.Add($message)
        Write-Host $message -ForegroundColor Red
    }
}

if ($Failures.Count -gt 0) {
    Write-Host "`nBuild/deploy completed with $($Failures.Count) failure(s):" -ForegroundColor Red

    foreach ($failure in $Failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }

    Write-Host "`nCeleste was NOT restarted because at least one workspace mod failed." -ForegroundColor Yellow
    exit 1
}

Write-Host "`nAll detected workspace mods built, packaged, and deployed successfully!" -ForegroundColor Green
Write-Host "Restarting Celeste..." -ForegroundColor Yellow

Start-Process `
    -FilePath $CelesteExe `
    -WorkingDirectory $CelestePath
