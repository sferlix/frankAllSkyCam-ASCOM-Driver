@echo off
rem Thin launcher for the Scheduled Task: keeps the task's own command line free of PowerShell's
rem -File/-ExecutionPolicy arguments, since Inno Setup, schtasks, and PowerShell would otherwise
rem all need to agree on the same nested quoting for a path that itself contains spaces.
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Watchdog.ps1"
