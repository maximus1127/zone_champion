@echo off
rem Builds Zone Champion and installs it for the current user. Double-click this file; nothing needs to be installed first.
rem -ExecutionPolicy Bypass lets the script run on PCs that block PowerShell scripts by default.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Publish.ps1" -Install
pause
