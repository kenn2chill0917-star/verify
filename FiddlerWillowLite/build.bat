@echo off
setlocal
set "PY=python"
where py >nul 2>nul
if %errorlevel% equ 0 set "PY=py -3"
%PY% "%~dp0build.py" %*
exit /b %errorlevel%
