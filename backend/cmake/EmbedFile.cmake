# Writes OUTPUT: a C++ source with a function FUNCTION that returns the bytes of INPUT as a string. Run by CMake at build
# time, so the launcher carries its assets and needs no files beside it.
file(READ "${INPUT}" hex HEX)
string(REGEX REPLACE "([0-9a-f][0-9a-f])" "0x\\1," bytes "${hex}")
file(WRITE "${OUTPUT}"
"// Generated from ${INPUT} by backend/cmake/EmbedFile.cmake. Do not edit.
#include <string>

#include \"EmbeddedAssets.h\"

namespace gwl {

std::string ${FUNCTION}() {
    static const unsigned char bytes[] = {${bytes}};
    return std::string(reinterpret_cast<const char*>(bytes), sizeof(bytes));
}

}  // namespace gwl
")
