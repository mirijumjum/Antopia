@echo off
rem Doble clic (o ejecutar desde la carpeta del proyecto) para compilar el APK. Argumentos: -Copy
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-apk.ps1" %*
pause
