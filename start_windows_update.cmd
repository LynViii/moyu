@echo off
setlocal

set "MOYU_DIR=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$n=[string]([char]0x6478)+[string]([char]0x9c7c)+'.exe'; Start-Process -FilePath (Join-Path $env:MOYU_DIR $n)"
