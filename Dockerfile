# Builds the launcher from the sources copied in, then exports bin/ to the host.
# The builder image comes from Dockerfile.toolchain; build.sh and build.ps1 build it first.
ARG BUILDER_IMAGE=gwdevhub-builder:latest
FROM ${BUILDER_IMAGE} AS build

ARG PRESETS="linux-release windows-release"
ARG GWL_BUILD_NUMBER=0

WORKDIR /src
COPY backend backend
COPY assets assets
COPY frontend/dist frontend/dist

# The build folders persist between builds, so only the changed files compile. An interrupted compile leaves an empty object
# that looks up to date, so empty objects are removed first to make them compile again.
RUN --mount=type=cache,target=/src/backend/build-docker,sharing=locked \
    cd backend \
 && find build-docker \( -name '*.o' -o -name '*.obj' \) -size 0 -delete \
 && for preset in $PRESETS; do \
      cmake --preset "$preset" -B "build-docker/$preset" -DGWL_BUILD_NUMBER="$GWL_BUILD_NUMBER" \
      && cmake --build "build-docker/$preset" || exit 1; \
    done

FROM scratch AS export
COPY --from=build /src/bin/ /
