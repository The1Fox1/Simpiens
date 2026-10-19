#!/usr/bin/env bash
# Sets up a user-local toolchain for building the Android client (Epic CLIENT): a .NET 10 SDK with the android
# workload, a portable JDK 21 and the Android SDK packages the build asks for. Everything goes under
# $SIMPIENS_TOOLCHAINS (default ~/.cache/simpiens-toolchains): no sudo, nothing on the system PATH, and deleting the
# folder removes it all. About 2 GB. Then build with scripts/build-android.sh.
set -euo pipefail

T="${SIMPIENS_TOOLCHAINS:-$HOME/.cache/simpiens-toolchains}"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
mkdir -p "$T"

if [ ! -x "$T/dotnet/dotnet" ]; then
  echo "== .NET 10 SDK -> $T/dotnet"
  curl -sSL -o "$T/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
  bash "$T/dotnet-install.sh" --channel 10.0 --install-dir "$T/dotnet" --no-path
fi

echo "== android workload"
"$T/dotnet/dotnet" workload install android

if [ ! -x "$T/jdk/bin/javac" ]; then
  echo "== JDK 21 -> $T/jdk"
  mkdir -p "$T/jdk"
  curl -sSL https://aka.ms/download-jdk/microsoft-jdk-21-linux-x64.tar.gz | tar -xz -C "$T/jdk" --strip-components=1
fi

echo "== Android SDK packages -> $T/android-sdk"
JAVA_HOME="$T/jdk" "$T/dotnet/dotnet" build "$REPO/dotnet/Simpiens.Android" -t:InstallAndroidDependencies -f net10.0-android \
  -p:AndroidSdkDirectory="$T/android-sdk" -p:JavaSdkDirectory="$T/jdk" -p:AcceptAndroidSdkLicenses=True

echo "Toolchain ready in $T. Build with scripts/build-android.sh."
