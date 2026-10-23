#!/usr/bin/env bash
# Runs the KNI client's tests in the pinned image of scripts/client-tests.Containerfile (Epic CLIENT, review T2), so the
# render references are compared with the same Mesa and SDL2 locally and in CI.
#
#   scripts/test-client-in-container.sh            run the client's tests
#   scripts/test-client-in-container.sh --record   re-record the render references (look at render-output/ first, and
#                                                  commit them on their own)
#
# The working tree (not only what is committed) is copied into the container, built and tested there, so the
# host's bin/ and obj/ are never touched. The renders come back to container-output/ (git-ignored).
# CONTAINER_ENGINE=docker uses docker (CI); the default is podman, rootless.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENGINE="${CONTAINER_ENGINE:-podman}"
RECORD=0
case "${1:-}" in
  "") ;;
  --record) RECORD=1 ;;
  *) echo "usage: $0 [--record]" >&2; exit 64 ;;
esac

# The image is named by its recipe, so a changed Containerfile builds a new one and an unchanged one is reused
TAG="simpiens-client-tests:$(sha256sum "$REPO/scripts/client-tests.Containerfile" | cut -c1-12)"
if ! "$ENGINE" image inspect "$TAG" > /dev/null 2>&1; then
  "$ENGINE" build -t "$TAG" -f "$REPO/scripts/client-tests.Containerfile" "$REPO/scripts"
fi

OUT="$REPO/container-output"
rm -rf "$OUT"
mkdir -p "$OUT"

# The sources the .NET solution compiles: dotnet/ (without its build output), the core and harness under Assets/, and
# the golden baselines. Unity's Library/ and the host's builds stay out.
tar -C "$REPO" --exclude='bin' --exclude='obj' -czf "$OUT/sources.tgz" dotnet Assets/Simpiens.Core Assets/Simpiens.Core.Testing Baselines

"$ENGINE" run --rm \
  -v "$OUT":/out:Z \
  -e SIMPIENS_RECORD_RENDERS="$RECORD" \
  "$TAG" bash -c '
    set -euo pipefail
    tar -xzf /out/sources.tgz -C /work
    status=0
    xvfb-run -a -s "-screen 0 1280x1024x24" dotnet test dotnet/Simpiens.Client.Tests -c Release || status=$?
    cp -r dotnet/Simpiens.Client.Tests/bin/Release/net10.0/render-output /out/ 2>/dev/null || true
    if [ "$SIMPIENS_RECORD_RENDERS" = "1" ]; then cp dotnet/Simpiens.Client.Tests/Baselines/*.png /out/; fi
    exit $status
  '

if [ "$RECORD" = "1" ]; then
  cp "$OUT"/*.png "$REPO/dotnet/Simpiens.Client.Tests/Baselines/"
  echo "Re-recorded the references from the container; look at them (git diff --stat, render-output/) before committing."
fi
echo "Renders: $OUT/render-output"
