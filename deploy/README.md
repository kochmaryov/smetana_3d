# deploy/ — релизная механика smetana

Скрипты и конфигурации сборки дистрибутивов, SBOM, офлайн-аудита и подписи.
Полный процесс и обоснования решений — [docs/release.md](../docs/release.md).

| Файл | Назначение |
|---|---|
| [publish.ps1](publish.ps1) | Self-contained публикация win-x64/linux-x64 + раскладка нативных библиотек (Manifold, lib3mf) в `runtimes/<rid>/native/` + юридические документы + SHA256SUMS → `artifacts/publish/<rid>/` |
| [new-sbom.ps1](new-sbom.ps1) | SBOM (CycloneDX 1.5) + инвентаризация лицензий + сверка с политикой MIT/Apache/BSD/Boost → `artifacts/sbom/` |
| [verify-offline.ps1](verify-offline.ps1) | Аудит офлайн-режима: поиск сетевых API в `src/` (+ URL-скан среза) → `artifacts/offline-audit/` |
| [sign.ps1](sign.ps1) | Код-сигнинг (заготовка): signtool + RFC3161 на Windows, GPG + SHA256SUMS на Linux |
| [windows/smetana.iss](windows/smetana.iss) | Установщик Windows (Inno Setup 6, пер-пользовательская установка) |
| [windows/build-installer.ps1](windows/build-installer.ps1) | Сборка и подпись установщика Windows |
| [windows/build-update-package.ps1](windows/build-update-package.ps1) | Пакеты обновления для установленной копии: полный (фолбэк) + дельта от базовой версии → `artifacts/updates/` |
| [windows/apply-update.ps1](windows/apply-update.ps1) | Применение пакета обновления без переустановки (копируется внутрь пакета; резервные копии, откат) |
| [windows/update.cmd](windows/update.cmd) | Запуск обновления двойным щелчком (шаблон, копируется в пакет) |
| [linux/build-deb.sh](linux/build-deb.sh) | Сборка deb-пакета (Ubuntu 22.04+, dpkg-deb) |
| [linux/smetana.desktop](linux/smetana.desktop) | Desktop-интеграция Linux |

## Быстрый старт

```powershell
# 1. Нативные зависимости (один раз; бинарники в git не коммитятся):
powershell -ExecutionPolicy Bypass -File spikes\Spike.Manifold\build-native.ps1   # Manifold 3.5.4
powershell -ExecutionPolicy Bypass -File src\SmetaNA.Manifold\fetch-native.ps1
powershell -ExecutionPolicy Bypass -File src\SmetaNA.Formats\ThreeMf\native\fetch-lib3mf.ps1 -Runtime all

# 2. Срезы приложения (артефакты в artifacts/, в git не коммитятся):
powershell -ExecutionPolicy Bypass -File deploy\publish.ps1 -RequireNatives

# 3. Контроль качества релиза:
powershell -ExecutionPolicy Bypass -File deploy\new-sbom.ps1
powershell -ExecutionPolicy Bypass -File deploy\verify-offline.ps1 -PublishedDir artifacts\publish\win-x64

# 4. Установщики:
powershell -ExecutionPolicy Bypass -File deploy\windows\build-installer.ps1 -Version 1.0.0
./deploy/linux/build-deb.sh --version 1.0.0          # на Linux/WSL/контейнере ubuntu:22.04

# 5. Пакеты обновления для установленных копий (без переустановки, см. docs/release.md §8.1):
powershell -ExecutionPolicy Bypass -File deploy\windows\build-update-package.ps1 -Version 1.0.0
powershell -ExecutionPolicy Bypass -File deploy\windows\build-update-package.ps1 -Version 1.0.0 -Baseline <старый срез или установленный каталог>   # + дельта
```

Все скрипты поддерживают `-DryRun` / `--dry-run` (печатают команды без выполнения).
Секреты подписи — только через переменные окружения `SMETANA_SIGN_*` (см. sign.ps1).
