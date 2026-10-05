#!/usr/bin/env bash
# Builds the Minecraft side of the mod (mcrepo jar, Fabric 1.21.1).
#
#   ./scripts/build-mc-mod.sh
#   ./scripts/build-mc-mod.sh --minecraft-dir ~/.minecraft --install
#
# Requires: a JDK. Fabric Loom 1.18 (pinned by mc-mod/gradle.properties) needs
# JDK 25 to run Gradle; the mod itself is compiled to Java 21 bytecode.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MC_DIR=""
INSTALL=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --minecraft-dir) MC_DIR="$2"; shift 2 ;;
    --install) INSTALL=1; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

command -v java >/dev/null 2>&1 || { echo "java not found. Install a JDK (25+ recommended)." >&2; exit 1; }
echo "Using: $(java -version 2>&1 | head -n 1)"

cd "$ROOT/mc-mod"
chmod +x gradlew
./gradlew build --no-daemon

JAR="$(ls -1 "$ROOT/mc-mod/build/libs"/*.jar 2>/dev/null | grep -v -- '-sources.jar' | head -n 1)"
[[ -n "$JAR" ]] || { echo "Build finished but no jar was produced." >&2; exit 1; }
echo
echo "Built: $JAR"

if [[ "$INSTALL" == "1" ]]; then
  [[ -n "$MC_DIR" ]] || { echo "--install needs --minecraft-dir." >&2; exit 1; }
  mkdir -p "$MC_DIR/mods"
  cp "$JAR" "$MC_DIR/mods/"
  echo "Installed to: $MC_DIR/mods/$(basename "$JAR")"
fi
