@echo off
REM ============================================================
REM  Cuda Spirit 2.4.1 - verified Windows build and publish
REM  Produces a self-contained single-file CudaSpirit.exe.
REM ============================================================
setlocal enableextensions enabledelayedexpansion
title Cuda Spirit 2.4.1 - Build, Verify, and Publish

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\CudaSpirit.App\CudaSpirit.App.csproj"
set "SOLUTION=%ROOT%CudaSpirit.sln"
set "PUBLISH_DIR=%ROOT%publish"
set "EXE_NAME=CudaSpirit.exe"
set "EXE_PATH=%PUBLISH_DIR%\%EXE_NAME%"
set "RID=win-x64"
set "CONFIG=Release"
set "MIN_SDK_MAJOR=8"

set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "NUGET_XMLDOC_MODE=skip"

echo ============================================================
echo   CUDA SPIRIT 2.4.1 - VERIFIED BUILD AND PUBLISH
echo ============================================================

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [ERROR] A stable .NET 8 or newer SDK is not on PATH.
  goto :fail
)
for /f "usebackq delims=" %%V in (`dotnet --version 2^>nul`) do set "FOUND_SDK=%%V"
for /f "tokens=1 delims=." %%M in ("!FOUND_SDK!") do set "SDK_MAJOR=%%M"
if not defined SDK_MAJOR (
  echo [ERROR] Could not determine the installed .NET SDK version.
  goto :fail
)
if !SDK_MAJOR! LSS %MIN_SDK_MAJOR% (
  echo [ERROR] .NET SDK 8 or newer is required; found !FOUND_SDK!.
  goto :fail
)
echo       Using installed SDK !FOUND_SDK!.
if not exist "%SOLUTION%" (
  echo [ERROR] Solution not found: %SOLUTION%
  goto :fail
)
if not exist "%PROJECT%" (
  echo [ERROR] Project not found: %PROJECT%
  goto :fail
)

echo.
echo [1/7] Running source validation ...
python "%ROOT%tools\validate_source.py"
if errorlevel 1 (
  echo [ERROR] Source validation failed.
  goto :fail
)

echo.
echo [2/7] Closing running instances ...
taskkill /IM "%EXE_NAME%" /F >nul 2>&1
if errorlevel 1 (echo       none running.) else (echo       closed.)

echo.
echo [3/7] Cleaning previous output ...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%PUBLISH_DIR%" (
  echo [ERROR] Could not remove %PUBLISH_DIR%. A file may still be locked.
  goto :fail
)
call dotnet clean "%SOLUTION%" -c %CONFIG% --nologo >nul
if errorlevel 1 (
  echo [ERROR] dotnet clean failed.
  goto :fail
)

echo.
echo [4/7] Restoring dependencies ...
call dotnet restore "%SOLUTION%" --nologo
if errorlevel 1 (
  echo [ERROR] dotnet restore failed.
  goto :fail
)

echo.
echo [5/7] Compiling Release with warnings as errors ...
call dotnet build "%SOLUTION%" -c %CONFIG% --no-restore -warnaserror --nologo
if errorlevel 1 (
  echo [ERROR] dotnet build failed.
  goto :fail
)

echo.
echo [6/7] Publishing self-contained single-file Windows x64 app ...
call dotnet publish "%PROJECT%" -c %CONFIG% -r %RID% --self-contained true -warnaserror -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:DebugSymbols=false -o "%PUBLISH_DIR%" --nologo
if errorlevel 1 (
  echo [ERROR] dotnet publish failed.
  goto :fail
)

del /q "%PUBLISH_DIR%\*.pdb" "%PUBLISH_DIR%\*.xml" >nul 2>&1
if not exist "%EXE_PATH%" (
  echo [ERROR] Expected executable was not produced: %EXE_PATH%
  goto :fail
)
set "STRAY=0"
for %%F in ("%PUBLISH_DIR%\*.*") do (
  if /I not "%%~nxF"=="%EXE_NAME%" set /a STRAY+=1
)
if not "!STRAY!"=="0" (
  echo [ERROR] Publish output contains !STRAY! unexpected sidecar file^(s^).
  dir /b "%PUBLISH_DIR%"
  goto :fail
)

echo.
echo [7/7] Launching and waiting for a real main window ...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $p=Start-Process -FilePath '%EXE_PATH%' -PassThru; $deadline=(Get-Date).AddSeconds(60); $shown=$false; while((Get-Date)-lt $deadline){Start-Sleep -Milliseconds 500; $p.Refresh(); if($p.HasExited){throw ('Application exited early with code '+$p.ExitCode)}; if($p.MainWindowHandle -ne 0){$shown=$true; break}}; if(-not $shown){Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; throw 'No main window appeared within 60 seconds.'}; Stop-Process -Id $p.Id -Force; Write-Host '      [OK] Main window appeared and closed cleanly.'"
if errorlevel 1 (
  echo [ERROR] Launch verification failed.
  goto :fail
)

for %%A in ("%EXE_PATH%") do set "EXE_SIZE=%%~zA"
set /a EXE_MB=!EXE_SIZE! / 1048576 2>nul
echo.
echo ============================================================
echo   SUCCESS - VERIFIED CUDA SPIRIT 2.4.1 BUILD
echo   %EXE_PATH% ^(~!EXE_MB! MB^)
echo ============================================================
echo.
pause
exit /b 0

:fail
echo.
echo ============================================================
echo   BUILD FAILED - NOTHING WAS PRESENTED AS VERIFIED
echo ============================================================
echo.
pause
exit /b 1
