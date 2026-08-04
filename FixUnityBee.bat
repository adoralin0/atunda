@echo off
REM Fix stuck Unity Bee build backend (false "compile errors").
REM Close Unity completely before running this.

echo Stopping leftover Unity build processes...
taskkill /F /IM bee_backend.exe >nul 2>&1
taskkill /F /IM Unity.ILPP.Runner.exe >nul 2>&1

set "BEE=%~dp0Library\Bee"
if not exist "%BEE%" (
  echo Library\Bee not found. Is this the project root?
  pause
  exit /b 1
)

echo Clearing Bee lock/state files...
del /F /Q "%BEE%\TundraBuildState.state" >nul 2>&1
del /F /Q "%BEE%\TundraBuildState.state.map" >nul 2>&1
del /F /Q "%BEE%\tundra.digestcache" >nul 2>&1
del /F /Q "%BEE%\tundra.digestcache.tmp" >nul 2>&1
del /F /Q "%BEE%\tundra.log.json" >nul 2>&1
del /F /Q "%BEE%\backend*.traceevents" >nul 2>&1
del /F /Q "%BEE%\bee_backend.info" >nul 2>&1

echo Done.
echo.
echo Reopen the project in ONE Unity Editor, wait for scripts to finish compiling, then build again.
pause
