#include "DataUrl.h"

#include "utils/Strings.h"

namespace gwl {

std::string pngDataUrl(const std::string& png) {
    const std::vector<std::uint8_t> bytes(png.begin(), png.end());
    return "data:image/png;base64," + base64Encode(bytes);
}

}  // namespace gwl
