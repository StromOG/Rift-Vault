@echo off
title Rift Vault - Modern AI File Explorer
cd /d "%~dp0"
taskkill /f /im RiftVault.exe >nul 2>&1
dotnet run --no-build
