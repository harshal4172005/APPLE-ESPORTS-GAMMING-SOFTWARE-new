@echo off
title Auto-Commit System
set "intervalSeconds=120"
set "remote=new-origin"
set "branch=main"

color 0B
echo ========================================
echo    Auto-Commit System Started!          
echo ========================================
echo Monitoring directory for changes every %intervalSeconds% seconds.
echo Press Ctrl+C to stop.
echo.

:loop
git status --porcelain > "%temp%\git_status.txt"
for %%A in ("%temp%\git_status.txt") do (
    if %%~zA gtr 0 (
        color 0A
        echo [ %date% %time% ] Changes detected! Committing and pushing...
        git add -A
        git commit -m "Auto-commit: %date% %time%"
        git push %remote% %branch%
        echo [ %date% %time% ] Successfully pushed to GitHub.
        echo.
        color 0B
    )
)

timeout /t %intervalSeconds% /nobreak >nul
goto loop
