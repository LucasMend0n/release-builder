@echo off
setlocal enableextensions

set "APP_NAME=release-builder"
set "EXE_NAME=release-builder.exe"
set "INSTALL_DIR=%LOCALAPPDATA%\Programs\%APP_NAME%"
set "CONFIG_DIR=%APPDATA%\%APP_NAME%"
set "CONFIG_FILE=%CONFIG_DIR%\appsettings.json"
set "SRC_EXE=%~dp0%EXE_NAME%"
set "SRC_TEMPLATE=%~dp0examples\appsettings.template.json"

echo.
echo === Instalando %APP_NAME% ===
echo.

if not exist "%SRC_EXE%" (
    echo [FAIL] %EXE_NAME% nao encontrado em %~dp0
    echo        Rode este script de dentro da pasta extraida do ZIP.
    exit /b 1
)

if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
copy /Y "%SRC_EXE%" "%INSTALL_DIR%\%EXE_NAME%" >nul
if errorlevel 1 (
    echo [FAIL] Falha ao copiar o executavel para %INSTALL_DIR%
    exit /b 1
)
echo [OK]  Executavel instalado em %INSTALL_DIR%

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$dir = [Environment]::GetEnvironmentVariable('Path', 'User');" ^
  "if ($null -eq $dir) { $dir = '' };" ^
  "$parts = $dir.Split(';') | Where-Object { $_ -ne '' };" ^
  "if ($parts -notcontains '%INSTALL_DIR%') {" ^
  "  $new = (@($parts) + '%INSTALL_DIR%') -join ';';" ^
  "  [Environment]::SetEnvironmentVariable('Path', $new, 'User');" ^
  "  Write-Host '[OK]  Adicionado ao PATH do usuario';" ^
  "} else {" ^
  "  Write-Host '[OK]  Ja estava no PATH do usuario';" ^
  "}"
if errorlevel 1 (
    echo [FAIL] Falha ao atualizar o PATH do usuario
    exit /b 1
)

if not exist "%CONFIG_DIR%" mkdir "%CONFIG_DIR%"
if not exist "%CONFIG_FILE%" (
    if exist "%SRC_TEMPLATE%" (
        copy /Y "%SRC_TEMPLATE%" "%CONFIG_FILE%" >nul
        echo [OK]  Config criada em %CONFIG_FILE%
        echo [!]   Edite o appsettings.json antes de usar
    ) else (
        echo [WARN] Template nao encontrado, config nao foi criada
        echo        Rode release-builder uma vez para gerar o template padrao
    )
) else (
    echo [OK]  Config existente preservada em %CONFIG_FILE%
)

echo.
echo === Instalacao concluida ===
echo.
echo Reabra o terminal e rode:
echo   release-builder -v 1.5.0
echo.
endlocal
