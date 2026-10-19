// Linux: the distribution's pretty name from os-release, as the Rust launcher shows it.

#include "SystemInfo.h"

#include <fstream>
#include <sstream>

namespace gwl {

std::string osDescription() {
    std::ifstream release("/etc/os-release");
    std::string line;
    while (std::getline(release, line)) {
        const std::string key = "PRETTY_NAME=";
        if (line.rfind(key, 0) == 0) {
            std::string name = line.substr(key.size());
            if (name.size() >= 2 && name.front() == '"' && name.back() == '"') {
                name = name.substr(1, name.size() - 2);
            }
            return name;
        }
    }
    return "Linux";
}

}  // namespace gwl
