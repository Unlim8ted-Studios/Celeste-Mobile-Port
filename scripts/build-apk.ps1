$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$env:ANDROID_SDK_ROOT = (Resolve-Path (Join-Path $root ".android-sdk")).Path
$env:ANDROID_HOME = $env:ANDROID_SDK_ROOT
$env:GRADLE_USER_HOME = (Resolve-Path (Join-Path $root ".gradle-user-home")).Path

& (Join-Path $root ".gradle-home\gradle-8.11.1\bin\gradle.bat") -p (Join-Path $root "geckoview-wrapper") --no-daemon :app:assembleDebug
Copy-Item -LiteralPath (Join-Path $root "geckoview-wrapper\app\build\outputs\apk\debug\app-debug.apk") -Destination (Join-Path $root "celeste-fixed.apk") -Force
Get-Item (Join-Path $root "celeste-fixed.apk") | Select-Object FullName, Length, LastWriteTime
