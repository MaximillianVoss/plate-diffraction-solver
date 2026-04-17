@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0build_cpu.ps1" %*
exit /b %ERRORLEVEL%
