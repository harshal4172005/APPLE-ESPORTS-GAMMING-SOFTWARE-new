@echo off
echo ========================================================
echo   APPLE ESPORTS GAMING SOFTWARE - SYSTEM STARTUP
echo ========================================================
echo.
echo Stopping any old containers...
docker-compose down

echo.
echo Rebuilding system with the latest changes...
docker-compose build --no-cache

echo.
echo Starting the system...
docker-compose up -d

echo.
echo ========================================================
echo SYSTEM IS NOW RUNNING!
echo.
echo 1. Wait about 15-30 seconds for the database to boot.
echo 2. Open your web browser and go to:
echo    http://localhost:8081
echo.
echo If you want to view logs, run: docker-compose logs -f
echo To stop the system, run: docker-compose down
echo ========================================================
pause
