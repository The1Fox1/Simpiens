#!/usr/bin/env bash
# Vendors MemoryPack (the runtime and its source generator) into Assets/Plugins/MemoryPack.
# Usage: scripts/update-memorypack.sh [version]   (default: the pinned version below)
#
# MemoryPack is resolved together with the vendored ZLogger (one dependency closure), so no assembly ships twice:
# a dependency ZLogger already vendors (Assets/Plugins/ZLogger/VERSIONS.txt) is shared from there, and the script
# stops if MemoryPack needs a different version of it. Facades Unity provides are skipped, as in
# scripts/update-zlogger.sh. Run this again after updating ZLogger.
#
# The source generator (Analyzers/MemoryPack.Generator.dll) is a Roslyn analyzer. Its committed .meta carries the
# RoslynAnalyzer label and no platforms, so Unity runs it while compiling and never ships it in a player.
# The script replaces DLLs only, never .meta files.
set -euo pipefail

VERSION="${1:-1.21.4}"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="${PROJECT_DIR}/Assets/Plugins/MemoryPack"
ZLOGGER_VERSIONS="${PROJECT_DIR}/Assets/Plugins/ZLogger/VERSIONS.txt"
ZLOGGER_VERSION="$(awk '$1 == "ZLogger" { print $2 }' "${ZLOGGER_VERSIONS}")"
WORK="$(mktemp -d)"
trap 'rm -rf "${WORK}"' EXIT

cat > "${WORK}/restore.csproj" <<CSPROJ
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>netstandard2.1</TargetFramework></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ZLogger" Version="${ZLOGGER_VERSION}" />
    <PackageReference Include="MemoryPack" Version="${VERSION}" />
  </ItemGroup>
</Project>
CSPROJ

dotnet restore "${WORK}/restore.csproj" --packages "${WORK}/packages" -v q

mkdir -p "${DEST}/Analyzers"
find "${DEST}" -name "*.dll" -delete

python3 - "${WORK}" "${DEST}" "${ZLOGGER_VERSIONS}" "${VERSION}" "${ZLOGGER_VERSION}" <<'PY'
import json, os, re, shutil, sys
work, dest, zlogger_versions, version, zlogger_version = sys.argv[1:6]

# What ZLogger vendors ("Name Version (path)") and the facades it skips ("Excluded (provided by Unity): a, b")
shared, exclude = {}, set()
for line in open(zlogger_versions, encoding="utf-8"):
    if line.startswith("Excluded"):
        exclude = {n.strip() for n in line.split(":", 1)[1].split(",") if n.strip()}
        continue
    m = re.match(r"^(\S+) (\S+) \(", line)
    if m:
        shared[m.group(1)] = m.group(2)

assets = json.load(open(os.path.join(work, "obj", "project.assets.json")))
target = assets["targets"][".NETStandard,Version=v2.1"]
resolved = {key.split("/")[0]: (key.split("/")[1], entry) for key, entry in target.items()}

# MemoryPack's own dependency closure, at the versions the joint restore chose
needed = set()
def walk(name):
    if name in needed:
        return
    needed.add(name)
    for dep in (resolved[name][1].get("dependencies") or {}):
        walk(dep)
walk("MemoryPack")

manifest, problems = [], []
for name in sorted(needed):
    pkg_version, entry = resolved[name]
    dlls = [r for r in (entry.get("runtime") or {}) if r.endswith(".dll")]
    if not dlls:
        continue
    if name in exclude:
        manifest.append(f"{name} {pkg_version} (provided by Unity)")
        continue
    if name in shared:
        if shared[name] != pkg_version:
            problems.append(f"{name}: ZLogger vendors {shared[name]}, MemoryPack needs {pkg_version}")
        manifest.append(f"{name} {pkg_version} (shared: Assets/Plugins/ZLogger)")
        continue
    for rel in dlls:
        shutil.copy2(os.path.join(work, "packages", name.lower(), pkg_version, rel), dest)
        manifest.append(f"{name} {pkg_version} ({rel})")

if problems:
    sys.exit("Shared dependency mismatch; update ZLogger and MemoryPack together:\n  " + "\n  ".join(problems))

generator = os.path.join(work, "packages", "memorypack.generator", version, "analyzers", "dotnet", "cs", "MemoryPack.Generator.dll")
shutil.copy2(generator, os.path.join(dest, "Analyzers"))
manifest.append(f"MemoryPack.Generator {version} (analyzers/dotnet/cs/MemoryPack.Generator.dll, as Analyzers/MemoryPack.Generator.dll)")

with open(os.path.join(dest, "VERSIONS.txt"), "w", encoding="utf-8") as f:
    f.write("Vendored by scripts/update-memorypack.sh — do not edit by hand.\n")
    f.write(f"Resolved together with ZLogger {zlogger_version}: shared dependencies are vendored once, in Assets/Plugins/ZLogger.\n\n")
    f.write("\n".join(manifest) + "\n")
print("\n".join(manifest))
PY
