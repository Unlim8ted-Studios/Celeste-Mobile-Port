@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0buildWrappers.ps1" -Target Android %*
exit /b %ERRORLEVEL%
