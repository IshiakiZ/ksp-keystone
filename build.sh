#!/bin/bash
# Build Keystone against the local KSP install.
#
#   ./build.sh            build into GameData/Keystone here, then install it into KSP
#   ./build.sh check      compile only, install nothing
#   ./build.sh dist       build into GameData/ here without installing anything
#   ./build.sh shaders    pack the lens shader again from tools/shaderpack/lens.glsl (after changing it)
#
# KeystoneTUFX.dll (the lens done through the TUFX mod, for where Keystone's own shader cannot run) is built
# only if TUFX is installed in the game, since it is built against it; KeystoneCosts.dll (what each mod costs,
# measured through the Harmony mod) only if Harmony is. (That one was KeystoneHarmony.dll until 2026-11-20: CKAN takes
# any library with "Harmony" in its name for a copy of Harmony itself.)
# Needs the .NET SDK (`brew install dotnet`). Override the game location with KSP_DIR=/path/to/KSP.
set -euo pipefail

cd "$(dirname "$0")"
MODE="${1:-install}"
. tools/build/common.sh
OUT=GameData/Keystone
TUFX="$KSP_DIR/GameData/TUFX/Plugins/TUFX.dll"
HARMONY="$KSP_DIR/GameData/000_Harmony/0Harmony.dll"

build() {  # build <folder to build into>
  local into="$1"
  mkdir -p "$into/PluginData"
  compile "$into/Keystone.dll" "" src/Keystone
  cp src/Keystone/Shaders/*.bundle "$into/PluginData/"
  if [ -f "$TUFX" ]; then WITH="$into/Keystone.dll:$TUFX" compile "$into/KeystoneTUFX.dll" "" src/KeystoneTUFX
  else rm -f "$into/KeystoneTUFX.dll"; echo "left out: KeystoneTUFX (TUFX is not installed here to build it against)"; fi
  # (the companion under its old name, from a build here before 2026-11-20, goes: a download must not carry it)
  rm -f "$into/KeystoneHarmony.dll"
  if [ -f "$HARMONY" ]; then WITH="$into/Keystone.dll:$HARMONY" compile "$into/KeystoneCosts.dll" "" src/KeystoneCosts
  else rm -f "$into/KeystoneCosts.dll"; echo "left out: KeystoneCosts (Harmony is not installed here to build it against)"; fi
}

case "$MODE" in
  check)
    build "$TMP/Keystone"
    echo "ok: compiles"
    ;;
  dist)
    build "$OUT"
    echo "ok: built into $OUT"
    ;;
  install)
    build "$OUT"
    TO="$KSP_DIR/GameData/Keystone"
    mkdir -p "$TO/PluginData"
    cp "$OUT/Keystone.dll" "$TO/"
    # (its shaders go beside the settings the player has made there, which are left alone)
    cp "$OUT"/PluginData/*.bundle "$TO/PluginData/"
    rm -f "$TO/KeystoneTUFX.dll" "$TO/KeystoneCosts.dll" "$TO/KeystoneHarmony.dll"
    [ -f "$OUT/KeystoneTUFX.dll" ] && cp "$OUT/KeystoneTUFX.dll" "$TO/"
    [ -f "$OUT/KeystoneCosts.dll" ] && cp "$OUT/KeystoneCosts.dll" "$TO/"
    echo "ok: Keystone installed to $TO (restart KSP to load it)"
    ;;
  shaders)
    python3 tools/shaderpack/make_bundle.py "$KSP_DIR" src/Keystone/Shaders/lens.bundle lens
    ;;
  *)
    echo "usage: ./build.sh [install|check|dist|shaders]" >&2; exit 2
    ;;
esac
