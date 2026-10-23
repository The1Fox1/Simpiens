#!/usr/bin/env bash
# Compiles the Android client (Epic CLIENT; compile-only until M4) with the user-local toolchain from
# scripts/setup-android-toolchain.sh. Extra arguments go to dotnet build (default: -c Release).
set -euo pipefail

T="${SIMPIENS_TOOLCHAINS:-$HOME/.cache/simpiens-toolchains}"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [ ! -x "$T/dotnet/dotnet" ] || [ ! -x "$T/jdk/bin/javac" ] || [ ! -d "$T/android-sdk" ]; then
  echo "No Android toolchain in $T: run scripts/setup-android-toolchain.sh first." >&2
  exit 2
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 JAVA_HOME="$T/jdk"
args=("$@")
[ ${#args[@]} -eq 0 ] && args=(-c Release)
"$T/dotnet/dotnet" build "$REPO/dotnet/Simpiens.Android" "${args[@]}" -p:AndroidSdkDirectory="$T/android-sdk" -p:JavaSdkDirectory="$T/jdk"
