#!/usr/bin/env bash
set -eo pipefail

# -----------------------------------------------------------------------------
# Simpiens PlayMode Integration Test Runner
# Executes automated multi-agent scenarios headlessly via Unity Test Framework.
# -----------------------------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
LOGS_DIR="${PROJECT_DIR}/Logs"
RESULTS_FILE="${LOGS_DIR}/playmode-results.xml"
TEST_LOG="${LOGS_DIR}/playmode-tests.log"

mkdir -p "${LOGS_DIR}"

# Locate Unity executable
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

echo "================================================================="
echo " Simpiens: Running PlayMode Integration Test Suite"
echo " Unity Binary: ${UNITY_BIN}"
echo " Project:      ${PROJECT_DIR}"
echo " Results File: ${RESULTS_FILE}"
echo " Test Log:     ${TEST_LOG}"
echo "================================================================="

# Check for active Unity Editor lockfile
if [ -f "${PROJECT_DIR}/Temp/UnityLockfile" ]; then
    echo "WARNING: An active Unity Editor session was detected (Temp/UnityLockfile)."
    echo "Headless batchmode tests require exclusive project access or running tests inside the Editor Test Runner."
    echo "If you wish to run headlessly, please close the Unity Editor GUI first."
    exit 2
fi

# Execute PlayMode tests
set +e
"${UNITY_BIN}" \
    -batchmode \
    -projectPath "${PROJECT_DIR}" \
    -runTests \
    -testPlatform PlayMode \
    -testResults "${RESULTS_FILE}" \
    -logFile "${TEST_LOG}"
TEST_EXIT_CODE=$?
set -e

echo ""
echo "================================================================="
if [ ${TEST_EXIT_CODE} -eq 0 ]; then
    echo " SUCCESS: All PlayMode Integration Tests Passed!"
else
    echo " FAILURE: PlayMode Tests failed with exit code ${TEST_EXIT_CODE}."
    if [ -f "${RESULTS_FILE}" ]; then
        echo "Check results at: ${RESULTS_FILE}"
    fi
    echo "Check test execution log at: ${TEST_LOG}"
fi
echo "================================================================="

exit ${TEST_EXIT_CODE}
