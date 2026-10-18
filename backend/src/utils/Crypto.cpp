#include "utils/Crypto.h"

#include "Crypto.h"

namespace gwl {

std::string sha256Hex(const Bytes& data) {
    return hexEncode(sha256(data));
}

}  // namespace gwl
