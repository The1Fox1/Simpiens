#!/usr/bin/env bash
set -eo pipefail

# -----------------------------------------------------------------------------
# Simpiens Persistence Smoke Test (Milestone 1 save/load)
# Builds a Linux player with the persistence smoke test compiled in (SIMPIENS_PERSISTENCE_SMOKE, this build only),
# runs it headless, and checks that the game boots and round-trips a save through MemoryPack in a real player.
# Logs: Logs/persistence-smoke-build.log, Logs/persistence-smoke-player.log
#
# Usage: scripts/run-persistence-smoke.sh [build-dir]   (default: Builds/PersistenceSmoke, git-ignored)
# -----------------------------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
LOGS_DIR="${PROJECT_DIR}/Logs"
BUILD_DIR="${1:-${PROJECT_DIR}/Builds/PersistenceSmoke}"
PLAYER="${BUILD_DIR}/Simpiens.x86_64"
BUILD_LOG="${LOGS_DIR}/persistence-smoke-build.log"
PLAYER_LOG="${LOGS_DIR}/persistence-smoke-player.log"

mkdir -p "${LOGS_DIR}" "${BUILD_DIR}"

# Locate Unity executable (same lookup as run-playmode-tests.sh)
UNITY_BIN=""
if [ -x "/home/bfox/.local/bin/unity" ]; then
    UNITY_BIN="/home/bfox/.local/bin/unity"
elif command -v unity &> /dev/null; then
    UNITY_BIN="$(command -v unity)"
elif [ -x "/home/bfox/Unity/Hub/Editor/6000.4.3f1/Editor/Unity" ]; then
    UNITY_BIN="/home/bfox/Unity/Hub/Editor/6000.4.3f1/Editor/Unity"
else
    echo "ERROR: Unity executable not found!"
    exit 1
fi

if [ -f "${PROJECT_DIR}/Temp/UnityLockfile" ]; then
    echo "ERROR: the project is open in a Unity Editor (Temp/UnityLockfile). Close it first."
    exit 2
fi

echo "Building the smoke player: ${PLAYER}"
BUILD_EXIT=0
"${UNITY_BIN}" -batchmode -nographics -projectPath "${PROJECT_DIR}" -buildTarget Linux64 \
    -executeMethod Simpiens.Core.Editor.PersistenceSmokeBuild.BuildLinux -smokeBuildPath "${PLAYER}" \
    -logFile "${BUILD_LOG}" || BUILD_EXIT=$?
if [ "${BUILD_EXIT}" -ne 0 ] || [ ! -x "${PLAYER}" ]; then
    echo "FAIL: the build failed (exit ${BUILD_EXIT}); see ${BUILD_LOG}"
    grep -E "error CS|\[PersistenceSmokeBuild\]" "${BUILD_LOG}" | head -20 || true
    exit 1
fi

echo "Running the smoke player headless"
PLAYER_EXIT=0
timeout 180 "${PLAYER}" -batchmode -nographics -logFile "${PLAYER_LOG}" > /dev/null 2>&1 || PLAYER_EXIT=$?

grep -o "Session '[^']*' spawned[^\"]*" "${PLAYER_LOG}" | head -1 || true
grep -o "PERSISTENCE SMOKE: [A-Z]*.*" "${PLAYER_LOG}" | head -1 || true
if [ "${PLAYER_EXIT}" -eq 0 ] && grep -q "PERSISTENCE SMOKE: PASS" "${PLAYER_LOG}"; then
    echo "PASS: the player booted and round-tripped a save (exit 0)"
    exit 0
fi
echo "FAIL: player exit ${PLAYER_EXIT} (3 = round trip failed, 124 = timed out); see ${PLAYER_LOG}"
exit 1
