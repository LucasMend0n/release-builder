@echo off
setlocal enableextensions

set "APP_NAME=release-builder"
set "INSTALL_DIR=%LOCALAPPDATA%\Programs\%APP_NAME%"
set "CONFIG_DIR=%APPDATA%\%APP_NAME%"

echo.
echo === Desinstalando %APP_NAME% ===
echo.

if exist "%INSTALL_DIR%" (
    rmdir /S /Q "%INSTALL_DIR%"
    echo [OK]  Removido %INSTALL_DIR%
) else (
    echo [--]  Pasta de instalacao nao encontrada
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$dir = [Environment]::GetEnvironmentVariable('Path', 'User');" ^
  "if ($null -eq $dir) { exit 0 };" ^
  "$parts = $dir.Split(';') | Where-Object { $_ -ne '' -and $_ -ne '%INSTALL_DIR%' };" ^
  "$new = $parts -join ';';" ^
  "[Environment]::SetEnvironmentVariable('Path', $new, 'User');" ^
  "Write-Host '[OK]  Removido do PATH do usuario'"
if errorlevel 1 (
    echo [WARN] Falha ao atualizar o PATH do usuario
)

if exist "%CONFIG_DIR%" (
    echo [!]   Config preservada em %CONFIG_DIR%
    echo       Apague manualmente se quiser limpar tudo.
)

echo.
echo === Desinstalacao concluida ===
echo.
echo Reabra o terminal para o PATH refletir a mudanca.
echo.
endlocal
