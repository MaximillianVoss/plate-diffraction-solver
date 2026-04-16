@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0build_cuda.ps1" %*
exit /b %ERRORLEVEL%
