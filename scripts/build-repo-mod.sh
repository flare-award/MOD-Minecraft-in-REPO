#!/usr/bin/env bash
# Builds the R.E.P.O. side of the mod (MinecraftInRepo.dll, BepInEx plugin).
#
#   ./scripts/build-repo-mod.sh                                 # offline stub references
#   ./scripts/build-repo-mod.sh "/path/to/REPO"                 # against a real install
#   ./scripts/build-repo-mod.sh "/path/to/REPO" --install       # ...and copy to BepInEx/plugins
#   ./scripts/build-repo-mod.sh "" --gamelibs                   # community NuGet assemblies
#
# Requires: .NET SDK 8+ (`dotnet` in PATH).

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${CONFIGURATION:-Release}"
REPO_GAME_DIR_ARG="${1:-${REPO_GAME_DIR:-}}"
INSTALL=0
GAMELIBS=0
for arg in "$@"; do
  case "$arg" in
    --install) INSTALL=1 ;;
    --gamelibs) GAMELIBS=1 ;;
  esac
done

command -v dotnet >/dev/null 2>&1 || { echo "dotnet not found. Install the .NET SDK 8+: https://dotnet.microsoft.com/download" >&2; exit 1; }

cd "$ROOT"

if [[ "$GAMELIBS" == "1" ]]; then
  echo "== Building plugin against R.E.P.O.GameLibs.Steam (NuGet) =="
  dotnet build repo-mod/MinecraftInRepo.csproj -c "$CONFIG" -p:UseGameLibsNuGet=true --nologo
elif [[ -n "$REPO_GAME_DIR_ARG" ]]; then
  echo "== Building plugin against the game assemblies in '$REPO_GAME_DIR_ARG' =="
  dotnet build repo-mod/MinecraftInRepo.csproj -c "$CONFIG" -p:RepoGameDir="$REPO_GAME_DIR_ARG" --nologo
else
  echo "== Building offline reference stubs =="
  dotnet build repo-mod/Stubs/StubAssemblies.csproj -c "$CONFIG" --nologo
  echo "== Building plugin against the stubs =="
  dotnet build repo-mod/MinecraftInRepo.csproj -c "$CONFIG" --nologo
fi

DLL="$ROOT/repo-mod/bin/$CONFIG/MinecraftInRepo.dll"
[[ -f "$DLL" ]] || { echo "Build finished but $DLL is missing." >&2; exit 1; }
echo
echo "Built: $DLL"

if [[ "$INSTALL" == "1" ]]; then
  [[ -n "$REPO_GAME_DIR_ARG" ]] || { echo "--install needs a R.E.P.O. path." >&2; exit 1; }
  PLUGINS="$REPO_GAME_DIR_ARG/BepInEx/plugins"
  mkdir -p "$PLUGINS"
  cp "$DLL" "$PLUGINS/MinecraftInRepo.dll"
  echo "Installed to: $PLUGINS/MinecraftInRepo.dll"
fi
