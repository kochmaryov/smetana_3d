@echo off
rem Обновление smetana без переустановки (docs/release.md, раздел 8.1).
rem Питание от PowerShell 5.1+: просто заменяет файлы программы по манифесту пакета.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0apply-update.ps1" %*
set SMETANA_UPDATE_EXIT=%ERRORLEVEL%
echo.
if "%SMETANA_UPDATE_EXIT%"=="0" (
  echo [smetana] Обновление завершено успешно.
) else (
  echo [smetana] Ошибка обновления, код %SMETANA_UPDATE_EXIT%. Сообщения - выше.
)
pause
exit /b %SMETANA_UPDATE_EXIT%
