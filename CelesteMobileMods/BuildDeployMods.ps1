# Robust automation script to build, package, and deploy all Celeste mods.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$CelestePath = "D:\SteamLibrary\steamapps\common\Celeste"
$ModsDest = Join-Path $CelestePath "Mods"
$Root = $PSScriptRoot

$Modules = @(
    "MobileBridge",
    "MobileTweaks",
    "MouseUI",
    "MobileMultiplayer",
    "BetterMapEditor"
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

function Get-ModProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModPath,

        [Parameter(Mandatory = $true)]
        [string]$ModName
    )

    $preferred = @(
        (Join-Path $ModPath "$ModName.csproj"),
        (Join-Path $ModPath "Source$ModName.csproj")
    )

    foreach ($candidate in $preferred) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    $projects = @(
        Get-ChildItem -LiteralPath $ModPath -Filter "*.csproj" -File -ErrorAction SilentlyContinue
    )

    if ($projects.Count -eq 1) {
        return $projects[0].FullName
    }

    if ($projects.Count -eq 0) {
        throw "No .csproj was found in $ModPath"
    }

    $names = ($projects | ForEach-Object { $_.Name }) -join ", "
    throw "Multiple .csproj files were found in $ModPath and none matched '$ModName.csproj' or 'Source$ModName.csproj': $names"
}

function Get-BuiltDll {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot,

        [Parameter(Mandatory = $true)]
        [string]$AssemblyName
    )

    $preferred = @(
        (Join-Path $ProjectRoot "bin\$AssemblyName.dll"),
        (Join-Path $ProjectRoot "bin\Release\net8.0\$AssemblyName.dll"),
        (Join-Path $ProjectRoot "bin\Release\$AssemblyName.dll")
    )

    foreach ($candidate in $preferred) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    $binRoot = Join-Path $ProjectRoot "bin"
    if (Test-Path -LiteralPath $binRoot -PathType Container) {
        $matches = @(
            Get-ChildItem -LiteralPath $binRoot -Filter "$AssemblyName.dll" -File -Recurse -ErrorAction SilentlyContinue |
                Where-Object {
                    $_.FullName -notmatch '[\\/](ref|refint)[\\/]'
                } |
                Sort-Object LastWriteTime -Descending
        )

        if ($matches.Count -gt 0) {
            return $matches[0].FullName
        }
    }

    throw "Could not find built assembly '$AssemblyName.dll' under $ProjectRoot\bin"
}

function New-CleanDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function New-ZipFromDirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationZip
    )

    if (Test-Path -LiteralPath $DestinationZip) {
        Remove-Item -LiteralPath $DestinationZip -Force
    }

    Push-Location $SourceDirectory
    try {
        Compress-Archive -Path "*" -DestinationPath $DestinationZip -CompressionLevel Optimal -Force
    }
    finally {
        Pop-Location
    }
}

function Deploy-Zip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ZipPath
    )

    $destination = Join-Path $ModsDest ([System.IO.Path]::GetFileName($ZipPath))
    Write-Host "Deploying $([System.IO.Path]::GetFileName($ZipPath)) to $ModsDest..." -ForegroundColor Yellow
    Copy-Item -LiteralPath $ZipPath -Destination $destination -Force
}

if (!(Test-Path -LiteralPath $CelestePath -PathType Container)) {
    throw "Celeste directory does not exist: $CelestePath"
}

if (!(Test-Path -LiteralPath (Join-Path $CelestePath "Celeste.exe") -PathType Leaf)) {
    throw "Celeste.exe was not found in: $CelestePath"
}

if (!(Test-Path -LiteralPath $ModsDest -PathType Container)) {
    New-Item -ItemType Directory -Path $ModsDest -Force | Out-Null
}

if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found in PATH. Install the .NET SDK or add dotnet to PATH."
}

# 0. Close Celeste if open.
Write-Host "Checking for running Celeste process..." -ForegroundColor Yellow
$CelesteProc = Get-Process -Name "Celeste" -ErrorAction SilentlyContinue
if ($CelesteProc) {
    Write-Host "Closing Celeste..." -ForegroundColor Yellow
    $CelesteProc | Stop-Process -Force

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 100
        if (!(Get-Process -Name "Celeste" -ErrorAction SilentlyContinue)) {
            break
        }
    }

    if (Get-Process -Name "Celeste" -ErrorAction SilentlyContinue) {
        throw "Celeste is still running after Stop-Process."
    }
}

# 1. Remove only ZIPs this script owns.
# Do NOT delete every .zip in Mods; that would remove unrelated installed mods.
Write-Host "Cleaning previously deployed mod ZIPs..." -ForegroundColor Yellow
$OwnedZipNames = @("CelesteNet.zip") + @($Modules | ForEach-Object { "$_.zip" })

foreach ($zipName in $OwnedZipNames) {
    $deployedZip = Join-Path $ModsDest $zipName
    if (Test-Path -LiteralPath $deployedZip -PathType Leaf) {
        Remove-Item -LiteralPath $deployedZip -Force
    }
}

# 2. Build CelesteNet dependencies.
Write-Host "`n=== CelesteNet ===" -ForegroundColor Magenta
$CelesteNetRoot = Join-Path $Root "CelesteNet"

if (!(Test-Path -LiteralPath $CelesteNetRoot -PathType Container)) {
    throw "CelesteNet directory was not found: $CelesteNetRoot"
}

$CNProjects = @(
    "CelesteNet.Shared\CelesteNet.Shared.csproj",
    "CelesteNet.Client\CelesteNet.Client.csproj",
    "CelesteNet.Server\CelesteNet.Server.csproj"
)

foreach ($relativeProject in $CNProjects) {
    $projectPath = Join-Path $CelesteNetRoot $relativeProject

    if (!(Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Missing CelesteNet project: $projectPath"
    }

    Invoke-DotNetBuild -ProjectPath $projectPath
}

# 3. Package CelesteNet.Client as an Everest mod.
Write-Host "Packaging CelesteNet.Client as a mod..." -ForegroundColor Yellow
$CNTemp = Join-Path $CelesteNetRoot "temp\_dist"
New-CleanDirectory -Path $CNTemp
New-Item -ItemType Directory -Path (Join-Path $CNTemp "bin") -Force | Out-Null

try {
    $CNClientRoot = Join-Path $CelesteNetRoot "CelesteNet.Client"
    $CNSharedRoot = Join-Path $CelesteNetRoot "CelesteNet.Shared"
    $CNServerRoot = Join-Path $CelesteNetRoot "CelesteNet.Server"

    $CNClientDll = Get-BuiltDll -ProjectRoot $CNClientRoot -AssemblyName "CelesteNet.Client"
    $CNSharedDll = Get-BuiltDll -ProjectRoot $CNSharedRoot -AssemblyName "CelesteNet.Shared"
    $CNServerDll = Get-BuiltDll -ProjectRoot $CNServerRoot -AssemblyName "CelesteNet.Server"

    Copy-Item -LiteralPath $CNClientDll -Destination (Join-Path $CNTemp "bin\CelesteNet.Client.dll") -Force
    Copy-Item -LiteralPath $CNSharedDll -Destination (Join-Path $CNTemp "bin\CelesteNet.Shared.dll") -Force
    Copy-Item -LiteralPath $CNServerDll -Destination (Join-Path $CNTemp "bin\CelesteNet.Server.dll") -Force

    $CNYaml = @'
- Name: CelesteNet.Client
  Version: 2.0.0
  DLL: bin/CelesteNet.Client.dll
  Dependencies:
    - Name: Everest
      Version: 1.6418.0
'@

    $CNYaml | Set-Content -LiteralPath (Join-Path $CNTemp "everest.yaml") -Encoding UTF8

    $CNZipPath = Join-Path $CelesteNetRoot "CelesteNet.zip"
    New-ZipFromDirectoryContents -SourceDirectory $CNTemp -DestinationZip $CNZipPath
    Deploy-Zip -ZipPath $CNZipPath
}
finally {
    if (Test-Path -LiteralPath $CNTemp) {
        Remove-Item -LiteralPath $CNTemp -Recurse -Force
    }
}

# 4. Build, package, and deploy each independent mod.
$Failures = [System.Collections.Generic.List[string]]::new()

foreach ($Mod in $Modules) {
    Write-Host "`n=== Processing $Mod ===" -ForegroundColor Magenta

    $ModPath = Join-Path $Root $Mod

    try {
        if (!(Test-Path -LiteralPath $ModPath -PathType Container)) {
            throw "Mod directory does not exist: $ModPath"
        }

        $ProjPath = Get-ModProject -ModPath $ModPath -ModName $Mod
        Write-Host "Project: $ProjPath" -ForegroundColor DarkGray

        Invoke-DotNetBuild -ProjectPath $ProjPath

        $DllSource = Get-BuiltDll -ProjectRoot $ModPath -AssemblyName $Mod
        $YamlSource = Join-Path $ModPath "everest.yaml"

        if (!(Test-Path -LiteralPath $YamlSource -PathType Leaf)) {
            throw "Missing everest.yaml: $YamlSource"
        }

        Write-Host "Using DLL: $DllSource" -ForegroundColor DarkGray
        Write-Host "Packaging $Mod..." -ForegroundColor Yellow

        $TempPath = Join-Path $ModPath "temp_dist"
        New-CleanDirectory -Path $TempPath
        New-Item -ItemType Directory -Path (Join-Path $TempPath "bin") -Force | Out-Null

        try {
            Copy-Item -LiteralPath $DllSource -Destination (Join-Path $TempPath "bin\$Mod.dll") -Force
            Copy-Item -LiteralPath $YamlSource -Destination (Join-Path $TempPath "everest.yaml") -Force

            # Package common Everest asset/content directories automatically if present.
            # This keeps future Dialog, Graphics, Audio, Maps, Loenn, etc. content with the mod.
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

            foreach ($contentDirectory in $ContentDirectories) {
                $sourceContent = Join-Path $ModPath $contentDirectory
                if (Test-Path -LiteralPath $sourceContent -PathType Container) {
                    Copy-Item -LiteralPath $sourceContent -Destination $TempPath -Recurse -Force
                }
            }

            $ZipPath = Join-Path $ModPath "$Mod.zip"
            New-ZipFromDirectoryContents -SourceDirectory $TempPath -DestinationZip $ZipPath
            Deploy-Zip -ZipPath $ZipPath
        }
        finally {
            if (Test-Path -LiteralPath $TempPath) {
                Remove-Item -LiteralPath $TempPath -Recurse -Force
            }
        }

        Write-Host "$Mod completed successfully." -ForegroundColor Green
    }
    catch {
        $message = "$Mod failed: $($_.Exception.Message)"
        $Failures.Add($message)
        Write-Host $message -ForegroundColor Red
    }
}

# 5. Report results and restart only if everything succeeded.
if ($Failures.Count -gt 0) {
    Write-Host "`nBuild/deploy completed with $($Failures.Count) failure(s):" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }

    Write-Host "`nCeleste was NOT restarted because one or more mods failed." -ForegroundColor Yellow
    exit 1
}

Write-Host "`nAll mods built, packaged, and deployed successfully!" -ForegroundColor Green
Write-Host "Restarting Celeste..." -ForegroundColor Yellow
Start-Process -FilePath (Join-Path $CelestePath "Celeste.exe") -WorkingDirectory $CelestePath
