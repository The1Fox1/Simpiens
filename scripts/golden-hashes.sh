#!/usr/bin/env bash
set -eo pipefail

# -----------------------------------------------------------------------------
# Simpiens golden world hashes (Epic CORE C2)
#   scripts/golden-hashes.sh check [seed...]    compare each seed's run with Baselines/golden-world-hash.txt
#   scripts/golden-hashes.sh record [seed...]   re-record each seed's baseline (a deliberate behaviour change only)
# Each seed runs GoldenWorldHashScenario in its own Unity process (SIMPIENS_SEED). Close the Unity Editor first.
# Default seeds: the default seed (also checked by every full PlayMode run) and two more.
# -----------------------------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
LOGS_DIR="${PROJECT_DIR}/Logs"
mkdir -p "${LOGS_DIR}"

MODE="${1:-check}"
shift || true
SEEDS=("$@")
if [ ${#SEEDS[@]} -eq 0 ]; then SEEDS=(20261009 20261010 424242); fi
if [ "${MODE}" != "check" ] && [ "${MODE}" != "record" ]; then
    echo "usage: $0 check|record [seed...]"; exit 64
fi

UNITY_BIN=""
if [ -x "/home/bfox/.local/bin/unity" ]; then UNITY_BIN="/home/bfox/.local/bin/unity"
elif command -v unity &> /dev/null; then UNITY_BIN="$(command -v unity)"
elif [ -x "/home/bfox/Unity/Hub/Editor/6000.4.3f1/Editor/Unity" ]; then UNITY_BIN="/home/bfox/Unity/Hub/Editor/6000.4.3f1/Editor/Unity"
else echo "ERROR: Unity executable not found!"; exit 1; fi

if [ -f "${PROJECT_DIR}/Temp/UnityLockfile" ]; then
    echo "ERROR: the Unity Editor has this project open (Temp/UnityLockfile). Close it first."; exit 2
fi

RECORD=0; [ "${MODE}" = "record" ] && RECORD=1
STATUS=0
for SEED in "${SEEDS[@]}"; do
    RESULTS="${LOGS_DIR}/golden-${MODE}-${SEED}.xml"
    LOG="${LOGS_DIR}/golden-${MODE}-${SEED}.log"
    set +e
    SIMPIENS_SEED="${SEED}" SIMPIENS_RECORD_GOLDEN="${RECORD}" "${UNITY_BIN}" -batchmode -projectPath "${PROJECT_DIR}" \
        -runTests -testPlatform PlayMode \
        -testFilter "Simpiens.Tests.PlayMode.SwarmPlayModeTests.Scenario_GoldenWorldHash_ReproducesTheBaseline" \
        -testResults "${RESULTS}" -logFile "${LOG}"
    CODE=$?
    set -e
    LINE=$(grep -o "\[GoldenWorldHashScenario\][^\"]*" "${LOG}" | head -1 || true)
    echo "seed ${SEED}: unity exit ${CODE} ${LINE}"
    [ ${CODE} -ne 0 ] && STATUS=1
done
exit ${STATUS}
