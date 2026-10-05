#!/usr/bin/env bash
# Downloads the prebuilt mods produced by GitHub Actions instead of building
# them locally (no .NET SDK / JDK / Minecraft decompilation needed).
#
#   ./scripts/fetch-builds.sh
#   ./scripts/fetch-builds.sh --out-dir ./dist
#
# Requires: GitHub CLI (https://cli.github.com/) once authenticated: `gh auth login`.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="$ROOT/dist"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --out-dir) OUT_DIR="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

command -v gh >/dev/null 2>&1 || { echo "gh (GitHub CLI) not found: https://cli.github.com/" >&2; exit 1; }

mkdir -p "$OUT_DIR"
echo "Looking for the latest successful 'build' run..."
RUN_ID="$(gh run list --workflow build.yml --status success --limit 1 --json databaseId --jq '.[0].databaseId')"
[[ -n "$RUN_ID" ]] || { echo "No successful build run found. Trigger one: gh workflow run build.yml" >&2; exit 1; }
echo "Using run $RUN_ID"

(cd "$OUT_DIR" && gh run download "$RUN_ID" -n MinecraftInRepo-gamelibs-build)
(cd "$OUT_DIR" && gh run download "$RUN_ID" -n mcrepo-fabric-mod)

find "$OUT_DIR" -name '*.dll' -o -name '*.jar' | sed 's/^/  /'
echo
echo "Artifacts are in $OUT_DIR"
