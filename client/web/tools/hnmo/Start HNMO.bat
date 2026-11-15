@echo off
rem Starts HNMO: the save game database, the game server with its admin panel,
rem and the game, which opens in your browser. Close this window to stop everything.
rem The window stays open whatever happens, so a problem can be read (and reported).
if not defined HNMO_WINDOW (
  set "HNMO_WINDOW=1"
  cmd /k ""%~f0" %*"
  exit /b
)
title HNMO - close this window to stop the game
cd /d "%~dp0"
if not exist "web\HNMO.dll" if not exist "web\HNMO.exe" goto :notunzipped
if not exist "Data\Monsters" goto :nodata
cd web
if exist "..\runtime\win-x64\dotnet.exe" goto :portable
HNMO.exe
goto :stopped
:portable
"..\runtime\win-x64\dotnet.exe" HNMO.dll
:stopped
echo.
echo HNMO has stopped. If it stopped by itself, the lines above say why
echo (the logs are in the "save\logs" folder). You can close this window.
goto :eof
:notunzipped
echo HNMO can't start from inside the zip file.
echo Right-click HNMO.zip, choose "Extract All...", then open the extracted HNMO folder
echo and double-click "Start HNMO.bat" there.
goto :eof
:nodata
echo The game data is missing. Copy the Data folder of the game into "%~dp0Data".
goto :eof
