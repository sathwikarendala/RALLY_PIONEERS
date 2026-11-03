@echo off
cd /d "%~dp0"
py -3 serve.py
if errorlevel 1 (
  echo.
  echo Could not start the local server. Install Python 3 and ensure the "py" launcher is available.
  pause
)
