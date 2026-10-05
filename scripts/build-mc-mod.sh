#!/usr/bin/env bash
# Builds the Minecraft side of the mod (mcrepo jar, Fabric 1.21.1).
#
#   ./scripts/build-mc-mod.sh
#   ./scripts/build-mc-mod.sh --minecraft-dir ~/.minecraft --install
#
# Requires: JDK 25+. Fabric Loom 1.18 (pinned by mc-mod/gradle.properties)
# needs JVM 25 to run Gradle - Java 21 fails with "Dependency requires at least
# JVM runtime version 25". The mod itself is compiled to Java 21 bytecode.
# Set JAVA_HOME to a JDK 25+ (or put it first on PATH) before running this.

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

command -v java >/dev/null 2>&1 || { echo "java not found. Install a JDK 25+." >&2; exit 1; }
JAVA_LINE="$(java -version 2>&1 | head -n 1)"
JAVA_MAJOR="$(echo "$JAVA_LINE" | sed -E 's/.*version "([0-9]+).*/\1/')"
echo "Using: $JAVA_LINE"
if [ -n "$JAVA_MAJOR" ] && [ "$JAVA_MAJOR" -lt 25 ] 2>/dev/null; then
  echo "" >&2
  echo "ERROR: Fabric Loom 1.18 needs JDK 25+ to run Gradle (found Java $JAVA_MAJOR)." >&2
  echo "Install Temurin 25 (https://adoptium.net/temurin/releases/?version=25) and either" >&2
  echo "  export JAVA_HOME=/path/to/jdk-25 ; export PATH=\"$JAVA_HOME/bin:$PATH\"" >&2
  echo "or set org.gradle.java.home in ~/.gradle/gradle.properties." >&2
  exit 1
fi

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
