@echo off
rem Starts HNMO: the save game database, the game server with its admin panel,
rem and the game, which opens in your browser.
rem Silent mode (tray icon, no console window) by default.
rem Visible console window: pass /window (or --window / /console), set HNMO_WINDOW=1,
rem or set "ConsoleWindow": true in save\hnmo-settings.json.
rem No blocks in brackets: a folder like "HNMO (1)" would end them early.
cd /d "%~dp0"
rem A problem at start (still in the zip, no game data) is shown in the window, which stays open.
if not exist "web\HNMO.dll" if not exist "web\HNMO.exe" goto :window
if not exist "Data\Monsters" if not exist "Data\packs" goto :window
if /i "%~1"=="/window" goto :window
if /i "%~1"=="--window" goto :window
if /i "%~1"=="/console" goto :window
if /i "%~1"=="--console" goto :window
if /i "%~1"=="-window" goto :window
if /i "%~1"=="--foreground" goto :window
if defined HNMO_WINDOW goto :window
if defined HNMO_CONSOLE goto :window
if exist "save\hnmo-settings.json" findstr /I /R /C:"\"ConsoleWindow\"[ ]*:[ ]*true" "save\hnmo-settings.json" >nul 2>nul && goto :window
if not exist "launcher\start-hidden.vbs" goto :window
start "" wscript.exe //nologo "%~dp0launcher\start-hidden.vbs"
exit /b
:window
if defined HNMO_WINDOW goto :start
set "HNMO_WINDOW=1"
cmd /k call "%~f0" /window
exit /b
:start
title HNMO - close this window to stop the game
cd /d "%~dp0"
if not exist "web\HNMO.dll" if not exist "web\HNMO.exe" goto :notunzipped
if not exist "Data\Monsters" if not exist "Data\packs" goto :nodata
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
