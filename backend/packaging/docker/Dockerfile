# Builds the launcher from the sources copied in, then exports bin/ to the host.
FROM ubuntu:24.04 AS build

ARG PRESETS="linux-release windows-release"
ARG GWL_BUILD_NUMBER=0
ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
 && apt-get install -y --no-install-recommends cmake ninja-build pkg-config g++ mingw-w64 ca-certificates curl unzip \
 && rm -rf /var/lib/apt/lists/*

# The WebView2 loader, carried in the Windows launcher. It's fetched from the WebView2 package and checked by hash.
RUN curl -sfL -o /tmp/webview2.nupkg https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/1.0.2903.40 \
 && echo "ef128016dd1e51c59178c827ed5b8aa3322c57afa8675d930f8109505542ad74  /tmp/webview2.nupkg" | sha256sum -c - \
 && mkdir -p /opt/webview2 \
 && unzip -q -j /tmp/webview2.nupkg build/native/x64/WebView2Loader.dll -d /opt/webview2 \
 && echo "462b36fd1be6ca9f7563466a89e57c41ef4a4def3e0a84fa885d203aea4a3aaf  /opt/webview2/WebView2Loader.dll" | sha256sum -c - \
 && rm /tmp/webview2.nupkg

WORKDIR /src
COPY backend backend
COPY assets assets
COPY frontend/dist frontend/dist

# The build folders persist between builds, so only the changed files compile. An interrupted compile leaves an empty object
# that looks up to date, so empty objects are removed first to make them compile again.
RUN --mount=type=cache,target=/src/backend/build-docker,sharing=locked \
    cd backend \
 && mkdir -p third_party/webview2/bin/x64 \
 && cp /opt/webview2/WebView2Loader.dll third_party/webview2/bin/x64/ \
 && find build-docker \( -name '*.o' -o -name '*.obj' \) -size 0 -delete \
 && for preset in $PRESETS; do \
      cmake --preset "$preset" -B "build-docker/$preset" -DGWL_BUILD_NUMBER="$GWL_BUILD_NUMBER" \
      && cmake --build "build-docker/$preset" || exit 1; \
    done

FROM scratch AS export
COPY --from=build /src/bin/ /
