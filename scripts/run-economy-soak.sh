#!/usr/bin/env bash
set -eo pipefail

# -----------------------------------------------------------------------------
# Simpiens Economy Soak Runner (Milestone 1 closure, Phase 3; review §3.3)
# Runs the explicit EconomySoakScenario (30 sim-minutes at 10x, no injection) once per
# (profile, seed, provisional export) and writes one CSV per run:
#   Logs/economy-soak-<profile>-seed<seed>-export<on|off>.csv
# A profile is a kind of player (fair, greedy, generous): the stall prices it sets at tick 0.
# Integrity assertions only (ledger conservation, horizon reached, prices applied, CSV written).
#
# Usage: scripts/run-economy-soak.sh [--profiles "fair greedy generous"] [--seeds "20261009 1 2"] [--export on|off|both]
# -----------------------------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
LOGS_DIR="${PROJECT_DIR}/Logs"
TEST_NAME="Scenario_EconomySoak_ObservationOnly"

SEEDS="20261009 1 2"
PROFILES="fair greedy generous"
EXPORT_MODE="on"
while [ $# -gt 0 ]; do
    case "$1" in
        --seeds) SEEDS="$2"; shift 2 ;;
        --profiles) PROFILES="$2"; shift 2 ;;
        --export) EXPORT_MODE="$2"; shift 2 ;;
        *) echo "Unknown argument: $1"; exit 64 ;;
    esac
done

case "${EXPORT_MODE}" in
    on) EXPORTS="1" ;;
    off) EXPORTS="0" ;;
    both) EXPORTS="1 0" ;;
    *) echo "--export must be on, off or both"; exit 64 ;;
esac

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
    echo "WARNING: An active Unity Editor session was detected (Temp/UnityLockfile). Close it to run headless soaks."
    exit 2
fi

mkdir -p "${LOGS_DIR}"
FAILED=0
CSVS=()

for PROFILE in ${PROFILES}; do
for SEED in ${SEEDS}; do
    for EXPORT in ${EXPORTS}; do
        LABEL="${PROFILE}-seed${SEED}-export$([ "${EXPORT}" = "1" ] && echo on || echo off)"
        echo "================================================================="
        echo " Economy soak: ${LABEL}"
        echo "================================================================="

        set +e
        SIMPIENS_SOAK_PROFILE="${PROFILE}" SIMPIENS_SEED="${SEED}" SIMPIENS_PROVISIONAL_EXPORT="${EXPORT}" "${UNITY_BIN}" \
            -batchmode \
            -projectPath "${PROJECT_DIR}" \
            -runTests \
            -testPlatform PlayMode \
            -testFilter "${TEST_NAME}" \
            -testResults "${LOGS_DIR}/economy-soak-${LABEL}.xml" \
            -logFile "${LOGS_DIR}/economy-soak-${LABEL}.log"
        CODE=$?
        set -e

        CSV="${LOGS_DIR}/economy-soak-${LABEL}.csv"
        if [ ${CODE} -ne 0 ]; then
            echo " FAILURE (exit ${CODE}): see ${LOGS_DIR}/economy-soak-${LABEL}.xml"
            FAILED=1
        elif [ ! -f "${CSV}" ]; then
            echo " FAILURE: test passed but ${CSV} is missing"
            FAILED=1
        else
            echo " OK: ${CSV}"
            grep -o "\[EconomySoakScenario\][^\"]*" "${LOGS_DIR}/economy-soak-${LABEL}.log" | head -1 || true
            CSVS+=("${CSV}")
        fi
    done
done
done

echo "================================================================="
echo " CSVs written:"
for C in "${CSVS[@]}"; do echo "   ${C}"; done
echo "================================================================="
exit ${FAILED}
