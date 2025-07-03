@echo off
setlocal

REM Feste Version setzen
set DOCKER_METADATA_OUTPUT_VERSION=dev

echo Starte docker-compose mit Version: %DOCKER_METADATA_OUTPUT_VERSION%

REM Compose mit gesetzter Umgebungsvariable starten
docker-compose up --build

endlocal
pause