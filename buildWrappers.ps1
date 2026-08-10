param(
    [ValidateSet("Android", "IOS", "All")]
    [string] $Target = "All",
    [switch] $NoBuild
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RuntimeSource = Join-Path $RepoRoot "CelesteRuntime"
$AndroidRuntimeDest = Join-Path $RepoRoot "AndroidWrapper\app\src\main\assets\CelesteRuntime"
$IOSRuntimeDest = Join-Path $RepoRoot "IOSWrapper\assets\CelesteRuntime"

function Copy-Runtime {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    if (-not (Test-Path -LiteralPath $RuntimeSource -PathType Container)) {
        throw "Missing runtime folder: $RuntimeSource"
    }

    $DestinationParent = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Force -Path $DestinationParent | Out-Null

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    Copy-Item -LiteralPath $RuntimeSource -Destination $Destination -Recurse -Force
    Write-Host "Staged CelesteRuntime -> $Destination"
}

function Remove-StagedRuntime {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
        Write-Host "Removed staged CelesteRuntime from $Destination"
    }
}

function Build-Android {
    try {
        Copy-Runtime -Destination $AndroidRuntimeDest

        if ($NoBuild) {
            return
        }

        $Gradle = Join-Path $RepoRoot "AndroidWrapper\gradlew.bat"
        if (-not (Test-Path -LiteralPath $Gradle -PathType Leaf)) {
            throw "Missing Android Gradle wrapper: $Gradle"
        }

        Push-Location (Join-Path $RepoRoot "AndroidWrapper")
        try {
            & $Gradle --no-daemon :app:assembleDebug
            if ($LASTEXITCODE -ne 0) {
                throw "Android wrapper build failed with exit code $LASTEXITCODE"
            }
        } finally {
            Pop-Location
        }
    } finally {
        Remove-StagedRuntime -Destination $AndroidRuntimeDest
    }
}

function Build-IOS {
    try {
        Copy-Runtime -Destination $IOSRuntimeDest

        if ($NoBuild) {
            return
        }

        $IOSRoot = Join-Path $RepoRoot "IOSWrapper"
        $Workspace = Get-ChildItem -LiteralPath $IOSRoot -Filter "*.xcworkspace" -ErrorAction SilentlyContinue | Select-Object -First 1
        $Project = Get-ChildItem -LiteralPath $IOSRoot -Filter "*.xcodeproj" -ErrorAction SilentlyContinue | Select-Object -First 1

        if ($null -eq $Workspace -and $null -eq $Project) {
            Write-Warning "No iOS Xcode workspace or project found under IOSWrapper. Runtime staging completed; skipping iOS build."
            return
        }

        $XcodeBuild = Get-Command xcodebuild -ErrorAction SilentlyContinue
        if ($null -eq $XcodeBuild) {
            Write-Warning "xcodebuild is not available on this machine. Runtime staging completed; skipping iOS build."
            return
        }

        Push-Location $IOSRoot
        try {
            if ($null -ne $Workspace) {
                & xcodebuild -workspace $Workspace.Name -scheme "Celeste" -configuration Debug build
            } else {
                & xcodebuild -project $Project.Name -scheme "Celeste" -configuration Debug build
            }

            if ($LASTEXITCODE -ne 0) {
                throw "iOS wrapper build failed with exit code $LASTEXITCODE"
            }
        } finally {
            Pop-Location
        }
    } finally {
        Remove-StagedRuntime -Destination $IOSRuntimeDest
    }
}

switch ($Target) {
    "Android" { Build-Android }
    "IOS" { Build-IOS }
    "All" {
        Build-Android
        Build-IOS
    }
}
