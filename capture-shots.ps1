# Herramienta de desarrollo: compila una build de Windows, la ejecuta y guarda una captura de cada pantalla
# en la carpeta Shots\ (main, nest_*, daily, roles, forager, builder, roam). Sirve para revisar la UI sin abrir Unity.
#
# Uso (desde la carpeta del proyecto):
#   .\capture-shots.ps1
#
# Requisitos: cerrar el editor de Unity con este proyecto abierto y tener el modulo Windows Mono instalado.
$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$log = Join-Path $project 'shots_build.log'
$shots = Join-Path $project 'Shots'

$versionLine = Select-String -Path (Join-Path $project 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion:' | Select-Object -First 1
$version = ($versionLine.Line -split ':')[1].Trim()
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "No encuentro Unity $version en '$unity'." }

$busy = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like "*$project*" }
$open = Get-Process -Name Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*Antopia*' }
if ($open -or $busy) { throw 'Hay una instancia de Unity usando este proyecto. Cierrala y vuelve a ejecutar el script.' }

Write-Host 'Compilando la build de Windows...' -ForegroundColor Cyan
$proc = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', "`"$project`"", '-buildTarget', 'Win64',
    '-executeMethod', 'Antopia.EditorTools.AntopiaBuild.BuildWindowsShots', '-logFile', "`"$log`"")
if ($proc.ExitCode -ne 0) {
    Select-String -Path $log -Pattern 'error CS' | Select-Object -Last 15 | ForEach-Object { $_.Line }
    throw "La build ha fallado (codigo $($proc.ExitCode)). Log: $log"
}

if (Test-Path $shots) { Remove-Item $shots -Recurse -Force }
Write-Host 'Haciendo capturas (se abre una ventana un momento)...' -ForegroundColor Cyan
Start-Process -FilePath (Join-Path $project 'Builds\Shots\Antopia.exe') -Wait -ArgumentList @(
    '-screen-width', '540', '-screen-height', '960', '-screen-fullscreen', '0', '-antopia-shots', "`"$shots`"")
Write-Host "Capturas en $shots" -ForegroundColor Green
Get-ChildItem $shots | ForEach-Object { $_.Name }
