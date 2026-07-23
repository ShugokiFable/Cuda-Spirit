@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build-NexusRelease.ps1"
if errorlevel 1 pause
