$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$modRoot = Join-Path $root "EverestAndroidPort"
$tmp = Join-Path $root "AndroidPort.new.zip"
$assetZip = Join-Path $root "assets\www\Mods\AndroidPort.zip"
$rootZip = Join-Path $root "AndroidPort.zip"

dotnet build (Join-Path $modRoot "AndroidPort.csproj") -c Debug -v minimal

if (Test-Path $tmp) {
    Remove-Item -LiteralPath $tmp -Force
}

Push-Location $modRoot
Compress-Archive -Path "metadata.yaml", "Dialog", "bin\AndroidPort.dll" -DestinationPath $tmp -Force
Pop-Location

Copy-Item -LiteralPath $tmp -Destination $assetZip -Force
Copy-Item -LiteralPath $tmp -Destination $rootZip -Force
Remove-Item -LiteralPath $tmp -Force

Get-Item $assetZip, $rootZip | Select-Object FullName, Length, LastWriteTime
