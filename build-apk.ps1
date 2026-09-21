# Compila el APK de Antopia (Android, IL2CPP, ARM64) sin abrir el editor.
#
# Uso (desde la carpeta del proyecto):
#   .\build-apk.ps1            -> genera Builds\Antopia.apk
#   .\build-apk.ps1 -Copy      -> ademas lo copia a apk\Antopia.apk (el que esta en git)
#
# Requisitos: cerrar antes el editor de Unity con este proyecto abierto.
# El log completo queda en build.log.
param(
    [switch]$Copy
)

$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$log = Join-Path $project 'build.log'
$apk = Join-Path $project 'Builds\Antopia.apk'

# Usa la version de Unity con la que esta hecho el proyecto.
$versionLine = Select-String -Path (Join-Path $project 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion:' | Select-Object -First 1
$version = ($versionLine.Line -split ':')[1].Trim()
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) {
    throw "No encuentro Unity $version en '$unity'. Instalala desde Unity Hub o cambia la ruta en este script."
}

# Editor abierto con la ventana del proyecto, u otra instancia (por ejemplo otra compilacion) usando este proyecto.
$open = Get-Process -Name Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*Antopia*' }
$busy = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like "*$project*" }
if ($open -or $busy) {
    throw 'Hay una instancia de Unity usando este proyecto (editor abierto u otra compilacion en curso). Cierrala, espera a que termine y vuelve a ejecutar el script.'
}

Write-Host "Compilando con Unity $version (tarda unos 5-10 minutos)..." -ForegroundColor Cyan
$started = Get-Date
# Sin -quit: AntopiaBuild.BuildAndroid cierra Unity por si mismo con el resultado.
$unityArgs = @(
    '-batchmode', '-nographics',
    '-projectPath', "`"$project`"",
    '-buildTarget', 'Android',
    '-executeMethod', 'Antopia.EditorTools.AntopiaBuild.BuildAndroid',
    '-logFile', "`"$log`""
)
$proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait -PassThru
$elapsed = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)

$fresh = (Test-Path $apk) -and ((Get-Item $apk).LastWriteTime -ge $started)
if ($proc.ExitCode -ne 0 -or -not $fresh) {
    Write-Host "La compilacion ha fallado (codigo $($proc.ExitCode), $elapsed min). Ultimos errores:" -ForegroundColor Red
    Select-String -Path $log -Pattern 'error CS|Build Player|BuildFailedException|\[AntopiaBuild\]|another Unity instance' | Select-Object -Last 15 | ForEach-Object { $_.Line }
    Write-Host "Log completo: $log"
    exit 1
}

$sizeMb = [math]::Round((Get-Item $apk).Length / 1MB, 1)
Write-Host "APK listo: $apk ($sizeMb MB, $elapsed min)" -ForegroundColor Green

if ($Copy) {
    Copy-Item $apk (Join-Path $project 'apk\Antopia.apk') -Force
    Write-Host 'Copiado a apk\Antopia.apk' -ForegroundColor Green
}
