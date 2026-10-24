#!/usr/bin/env bash
# ============================================================================
# smetana — сборка deb-пакета Linux x64 (Ubuntu 22.04+ и совместимые).
#
# Выбор deb вместо AppImage обоснован в docs/release.md (раздел «Установщики»):
# эталонный дистрибутив — Ubuntu 22.04+, установка через apt/dpkg без FUSE
# (AppImage требует libfuse2, которого нет по умолчанию на Ubuntu 24.04),
# системная интеграция (.desktop, меню, ассоциации), детерминированная сборка
# dpkg-deb без внешних инструментов. AppImage остаётся запасным вариантом
# «переносной» поставки и описан в docs/release.md.
#
# Использование:
#   ./deploy/linux/build-deb.sh --version 1.0.0 [--publish-dir DIR] [--output-dir DIR] [--dry-run]
#
# Собирать на самом старом поддерживаемом дистрибутиве (glibc 2.35 = Ubuntu 22.04)
# или в контейнере ubuntu:22.04 — иначе glibc-зависимость получится новее.
# ============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

PUBLISH_DIR="$REPO_ROOT/artifacts/publish/linux-x64"
OUTPUT_DIR="$REPO_ROOT/artifacts/installers"
VERSION=""
ARCH="amd64"
DRY_RUN=0
MAINTAINER="smetana <support@smetana.example.ru>"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --publish-dir) PUBLISH_DIR="$2"; shift 2 ;;
    --output-dir)  OUTPUT_DIR="$2"; shift 2 ;;
    --version)     VERSION="$2"; shift 2 ;;
    --arch)        ARCH="$2"; shift 2 ;;
    --maintainer)  MAINTAINER="$2"; shift 2 ;;
    --dry-run)     DRY_RUN=1; shift ;;
    -h|--help)     grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Неизвестный аргумент: $1" >&2; exit 1 ;;
  esac
done

if [[ -z "$VERSION" ]]; then
  VERSION="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$REPO_ROOT/Directory.Build.props")"
  VERSION="${VERSION:-0.0.0-dev}"
fi

PKG_NAME="smetana"
PKG_VERSION="${VERSION}"
PKG_FILE="$OUTPUT_DIR/${PKG_NAME}_${PKG_VERSION}_${ARCH}.deb"
ROOT="$OUTPUT_DIR/deb-root"

echo "smetana deb: version=$PKG_VERSION arch=$ARCH"
echo "  publish-dir: $PUBLISH_DIR"
echo "  результат:   $PKG_FILE"

if [[ "$DRY_RUN" -eq 1 ]]; then
  echo "[dry-run] План сборки:"
  echo "  1. $PUBLISH_DIR -> $ROOT/usr/lib/smetana/ (приложение + runtimes/<rid>/native + docs/legal)"
  echo "  2. $SCRIPT_DIR/smetana.desktop -> usr/share/applications/"
  echo "  3. обёртка usr/bin/smetana (exec /usr/lib/smetana/SmetaNA.EditorUI)"
  echo "  4. DEBIAN/control (Depends: libc6, libgl1, libx11-6, libxext6, libxrandr2, libxcursor1, libxi6, libxinerama1, libsm6, libice6, libfontconfig1, libxkbcommon0)"
  echo "  5. usr/share/doc/smetana/copyright + юридические документы из docs/legal"
  echo "  6. dpkg-deb --root-owner-group --build -> $PKG_FILE"
  echo "  7. sha256sum -> SHA256SUMS-linux64.txt"
  exit 0
fi

command -v dpkg-deb >/dev/null || { echo "Требуется dpkg-deb (Debian/Ubuntu или контейнер ubuntu:22.04)." >&2; exit 1; }
[[ -d "$PUBLISH_DIR" ]] || { echo "Не найден срез publish: $PUBLISH_DIR (deploy/publish.ps1 -Rid linux-x64)." >&2; exit 1; }

rm -rf "$ROOT"
mkdir -p "$ROOT/usr/lib/smetana" "$ROOT/usr/bin" "$ROOT/usr/share/applications" \
         "$ROOT/usr/share/doc/$PKG_NAME" "$ROOT/DEBIAN"

# 1. Приложение целиком: бинарники, нативные библиотеки (корень + runtimes/linux-x64/native), docs/legal.
cp -a "$PUBLISH_DIR"/. "$ROOT/usr/lib/smetana/"
chmod 0755 "$ROOT/usr/lib/smetana/SmetaNA.EditorUI"

# 2. Desktop-файл.
install -m 0644 "$SCRIPT_DIR/smetana.desktop" "$ROOT/usr/share/applications/smetana.desktop"

# 3. Обёртка в PATH.
cat > "$ROOT/usr/bin/smetana" <<'EOF'
#!/bin/sh
exec /usr/lib/smetana/SmetaNA.EditorUI "$@"
EOF
chmod 0755 "$ROOT/usr/bin/smetana"

# 4. Метаданные пакета. Depends — системные библиотеки Avalonia/X11/OpenGL
#    (self-contained .NET не требует пакетов dotnet; нативные Manifold/lib3mf
#    поставляются внутри пакета в runtimes/linux-x64/native/).
cat > "$ROOT/DEBIAN/control" <<EOF
Package: $PKG_NAME
Version: $PKG_VERSION
Section: graphics
Priority: optional
Architecture: $ARCH
Maintainer: $MAINTAINER
Depends: libc6 (>= 2.35), libgcc-s1, libstdc++6, libgl1, libx11-6, libxext6, libxrandr2, libxcursor1, libxi6, libxinerama1, libsm6, libice6, libfontconfig1, libxkbcommon0
Recommends: libgl1-mesa-dri
Homepage: https://smetana.example.ru
Description: 3D-редактор в духе SketchUp (офлайн, точные размеры, 3D-печать)
 Настольный 3D-редактор: push/pull, привязки, группы и компоненты,
 точные размеры, экспорт STL/3MF/OBJ, проверка печатаемости.
 Работает офлайн; лицензия — персональный ключ с офлайн-активацией.
EOF

# 5. Юридические документы (EULA, privacy, лицензии зависимостей).
if [[ -d "$REPO_ROOT/docs/legal" ]]; then
  cp -a "$REPO_ROOT/docs/legal/." "$ROOT/usr/share/doc/$PKG_NAME/legal/"
fi
cat > "$ROOT/usr/share/doc/$PKG_NAME/copyright" <<EOF
smetana — проприетарное ПО, лицензия: docs/legal/EULA.ru.md (EULA).
Third-party notices: docs/legal/third-party-licenses.md.
EOF

# 6. Сборка.
mkdir -p "$OUTPUT_DIR"
dpkg-deb --root-owner-group --build "$ROOT" "$PKG_FILE"

( cd "$OUTPUT_DIR" && sha256sum "$(basename "$PKG_FILE")" > SHA256SUMS-linux64.txt )
echo "Готово: $PKG_FILE"
cat "$OUTPUT_DIR/SHA256SUMS-linux64.txt"
echo "Подпись (заготовка): powershell -File deploy/sign.ps1 -Platform linux -Path $PKG_FILE"
