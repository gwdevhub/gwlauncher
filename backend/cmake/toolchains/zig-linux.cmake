# Compiles the Linux release with zig for glibc 2.35, so it runs on Ubuntu 22.04 and newer. Used by the linux-release-zig preset.
# The compiler wrappers in packaging/zig name the zig target.
get_filename_component(GWL_ZIG_DIR "${CMAKE_CURRENT_LIST_DIR}/../../packaging/zig" ABSOLUTE)
set(CMAKE_C_COMPILER "${GWL_ZIG_DIR}/zig-cc")
set(CMAKE_CXX_COMPILER "${GWL_ZIG_DIR}/zig-c++")
