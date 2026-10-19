#include "Stylesheets.h"
#include "utils/Files.h"

#include <fstream>
#include <sstream>

namespace gwl {

namespace {

const char* const kOverrideFile = "GWLauncher.override.css";

}  // namespace

std::string overrideCss(const std::filesystem::path& dir, const std::string& overrideTemplate) {
    const std::filesystem::path path = dir / kOverrideFile;
    std::error_code ignored;
    if (!std::filesystem::exists(path, ignored)) {
        writeFileBytes(path, bytesOf(overrideTemplate));
    }
    const auto bytes = readFileBytes(path);
    if (!bytes) {
        return {};
    }
    // The byte-order mark some editors write is not CSS, so it's dropped, as the C# launcher does.
    std::string text = withoutByteOrderMark(textOf(*bytes));
    return text;
}

std::string pageWithOverrides(const std::string& page, const std::string& overrides) {
    const auto blank = overrides.find_first_not_of(" \t\r\n");
    if (blank == std::string::npos) {
        return page;
    }
    const std::string style = "<style>" + overrides + "</style></head>";
    const auto head = page.find("</head>");
    if (head == std::string::npos) {
        return page;
    }
    return page.substr(0, head) + style + page.substr(head + std::string("</head>").size());
}

}  // namespace gwl
