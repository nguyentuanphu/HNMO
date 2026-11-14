@echo off
rem Starts HNMO: the save game database, the game server with its admin panel,
rem and the game, which opens in your browser. Close this window to stop everything.
title HNMO - close this window to stop the game
cd /d "%~dp0"
if not exist "Data\Monsters" (
  echo The game data is missing. Copy the Data folder of the game into "%~dp0Data".
  pause
  exit /b 1
)
cd web
rem The folder for every system has the .NET runtime in runtime\win-x64; a Windows-only folder has HNMO.exe.
if exist "..\runtime\win-x64\dotnet.exe" (
  "..\runtime\win-x64\dotnet.exe" HNMO.dll
) else (
  HNMO.exe
)
if errorlevel 1 (
  echo.
  echo HNMO stopped because of the problem shown above.
  pause
)
