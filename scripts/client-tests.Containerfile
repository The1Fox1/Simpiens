# The KNI client's tests in one pinned environment (Epic CLIENT, review T2): the render references are compared with
# what this image's Mesa (llvmpipe) and SDL2 draw, locally (scripts/test-client-in-container.sh, with podman) and in CI
# (with docker), so both see the same bytes. Everything that can move pixels is pinned:
#   - the base image, by digest (Ubuntu 24.04 of 2026-10-02);
#   - every apt package, by Ubuntu's archive snapshot of one moment (apt's --snapshot, snapshot.ubuntu.com);
#   - the .NET SDK, by version.
# Changing any of these is a deliberate change: rebuild, look at the renders, and re-record the references in their own
# commit (scripts/test-client-in-container.sh --record).
FROM docker.io/library/ubuntu:24.04@sha256:a2d43bffc46670d7108bb3c0468c67c5557c4c92e83e1e34cbff1322ad1b94dc

ARG APT_SNAPSHOT=20261015T000000Z
ARG DOTNET_SDK_VERSION=10.0.109

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_ROOT=/opt/dotnet \
    PATH=/opt/dotnet:$PATH \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_PACKAGES=/root/.nuget/packages

# The snapshot needs HTTPS certificates first, from the image's own (pinned) archive state
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates \
 && apt-get update --snapshot "$APT_SNAPSHOT" \
 && apt-get install -y --no-install-recommends --snapshot "$APT_SNAPSHOT" \
      curl libicu74 libssl3t64 \
      libsdl2-2.0-0 libgl1-mesa-dri libglx-mesa0 libegl1 xvfb xauth \
 && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --version "$DOTNET_SDK_VERSION" --install-dir /opt/dotnet \
 && rm /tmp/dotnet-install.sh \
 && dotnet --version

# The tests draw through X under Xvfb with Mesa's software renderer, as CI did before the image
ENV SDL_VIDEODRIVER=x11 \
    LIBGL_ALWAYS_SOFTWARE=1

WORKDIR /work
