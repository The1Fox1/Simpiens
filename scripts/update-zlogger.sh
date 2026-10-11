#!/usr/bin/env bash
# Re-vendors ZLogger and its netstandard2.1 dependency closure into Assets/Plugins/ZLogger.
# Usage: scripts/update-zlogger.sh [version]   (default: the pinned version below)
# Keep the ZLogger.Unity git tag in Packages/manifest.json in sync with this version.
# Known benign Editor message: "Duplicate assembly 'System.IO.Hashing.dll' with different versions detected".
# com.unity.collections ships a 9.0 copy in its test folder; the Editor prefers it, while player builds use
# the vendored 8.0 copy. Do not delete the vendored copy.
set -euo pipefail

VERSION="${1:-2.5.10}"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="${PROJECT_DIR}/Assets/Plugins/ZLogger"
WORK="$(mktemp -d)"
trap 'rm -rf "${WORK}"' EXIT

# Facades Unity's .NET Standard 2.1 profile already provides; vendoring them causes duplicate-type conflicts.
EXCLUDE="System.Buffers System.Memory System.Numerics.Vectors System.Threading.Tasks.Extensions"

cat > "${WORK}/restore.csproj" <<CSPROJ
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>netstandard2.1</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="ZLogger" Version="${VERSION}" /></ItemGroup>
</Project>
CSPROJ

dotnet restore "${WORK}/restore.csproj" --packages "${WORK}/packages" -v q

mkdir -p "${DEST}"
find "${DEST}" -name "*.dll" -delete

python3 - "${WORK}" "${DEST}" "${EXCLUDE}" <<'PY'
import json, os, shutil, sys
work, dest, exclude = sys.argv[1], sys.argv[2], set(sys.argv[3].split())
assets = json.load(open(os.path.join(work, "obj", "project.assets.json")))
target = assets["targets"][".NETStandard,Version=v2.1"]
manifest = []
for key, entry in sorted(target.items()):
    name, version = key.split("/")
    if name in exclude:
        continue
    for rel in (entry.get("runtime") or {}):
        if not rel.endswith(".dll"):
            continue
        src = os.path.join(work, "packages", name.lower(), version, rel)
        shutil.copy2(src, dest)
        manifest.append(f"{name} {version} ({rel})")
with open(os.path.join(dest, "VERSIONS.txt"), "w") as f:
    f.write("Vendored by scripts/update-zlogger.sh — do not edit by hand.\n")
    f.write("Excluded (provided by Unity): " + ", ".join(sorted(exclude)) + "\n\n")
    f.write("\n".join(manifest) + "\n")
print("\n".join(manifest))
PY
