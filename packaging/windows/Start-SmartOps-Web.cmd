@echo off
setlocal
cd /d "%~dp0"
if not exist "SmartOps.Web.exe" (
  echo SmartOps.Web.exe was not found. Extract the entire ZIP before launching.
  pause
  exit /b 1
)
echo Starting SmartOps Web on http://127.0.0.1:5245 ...
echo Keep the SmartOps Web window open while using the web interface.
start "SmartOps Web" /D "%~dp0" "%~dp0SmartOps.Web.exe"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$uri='http://127.0.0.1:5245/Account/Login'; for($i=0;$i -lt 30;$i++){try{$r=Invoke-WebRequest -UseBasicParsing -Uri $uri -TimeoutSec 2; if($r.StatusCode -eq 200){Start-Process $uri; exit 0}}catch{};Start-Sleep -Seconds 1};Write-Host 'Unable to open SmartOps Web. Check the SmartOps Web console for errors.';exit 1"
if errorlevel 1 pause
endlocal
