#!/bin/bash
# Baut vkd3d-compiler nativ für diesen Mac nach tools/shaderc/bin. Damit übersetzt
# der Content Builder die Shader in Content/Effects ohne Wine (siehe ./wine).
# Einmalig ausführen; braucht:
#   brew install vulkan-headers spirv-headers bison make
set -euo pipefail

VERSION=2.1
SHA256=7510146aff2adfb4ae07ab890701a607e5ff7c66e57100cfc9f630ec92eeda6a

dir="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

cd "$work"
curl -fsSLO "https://dl.winehq.org/vkd3d/source/vkd3d-$VERSION.tar.xz"
echo "$SHA256  vkd3d-$VERSION.tar.xz" | shasum -a 256 -c -
tar -xJf "vkd3d-$VERSION.tar.xz"
cd "vkd3d-$VERSION"

# Das Bison von macOS (2.3) ist zu alt, das make (3.81) kennt keine Gruppenziele (&:)
export PATH="$(brew --prefix bison)/bin:$PATH"
# Nur der Shader-Compiler wird gebaut; libvulkan bräuchte erst libvkd3d zur Laufzeit
./configure --disable-tests --disable-doxygen-doc --disable-shared \
    CPPFLAGS="-I$(brew --prefix)/include" SONAME_LIBVULKAN=libvulkan.dylib
# Erzeugte Header zuerst: das Ziel vkd3d-compiler allein hängt nicht davon ab
gmake include/private/vkd3d_version.h include/private/spirv_grammar.h \
    libs/vkd3d-shader/hlsl.tab.h libs/vkd3d-shader/preproc.tab.h
gmake -j"$(sysctl -n hw.ncpu)" vkd3d-compiler

mkdir -p "$dir/bin"
cp vkd3d-compiler "$dir/bin/"
"$dir/bin/vkd3d-compiler" --version
