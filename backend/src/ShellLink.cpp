#include "ShellLink.h"

#include <cstddef>
#include <stdexcept>

#include "utils/Files.h"

namespace gwl {

namespace {

const std::uint32_t kHasLinkTargetIdList = 0x01;
const std::uint32_t kHasLinkInfo = 0x02;
const std::uint32_t kHasName = 0x04;
const std::uint32_t kHasRelativePath = 0x08;
const std::uint32_t kHasWorkingDir = 0x10;
const std::uint32_t kHasArguments = 0x20;
const std::uint32_t kIsUnicode = 0x80;
const std::uint32_t kVolumeIdAndLocalBasePath = 0x01;
const std::size_t kHeaderSize = 0x4C;

[[noreturn]] void truncated() {
    throw std::runtime_error("The shortcut is truncated.");
}

std::uint16_t u16At(const std::vector<std::uint8_t>& data, std::size_t offset) {
    if (offset > data.size() || data.size() - offset < 2) {
        truncated();
    }
    return static_cast<std::uint16_t>(data[offset] | data[offset + 1] << 8);
}

std::uint32_t u32At(const std::vector<std::uint8_t>& data, std::size_t offset) {
    if (offset > data.size() || data.size() - offset < 4) {
        truncated();
    }
    return static_cast<std::uint32_t>(data[offset]) | static_cast<std::uint32_t>(data[offset + 1]) << 8 |
           static_cast<std::uint32_t>(data[offset + 2]) << 16 | static_cast<std::uint32_t>(data[offset + 3]) << 24;
}

std::u16string unitsAt(const std::vector<std::uint8_t>& data, std::size_t offset, std::size_t count) {
    std::u16string units;
    units.reserve(count);
    for (std::size_t i = 0; i < count; ++i) {
        units.push_back(static_cast<char16_t>(data[offset + 2 * i] | data[offset + 2 * i + 1] << 8));
    }
    return units;
}

// A zero-terminated string at offset, or nothing when the offset is outside the data.
std::optional<std::u16string> zeroTerminated(const std::vector<std::uint8_t>& data, std::size_t offset, bool unicode) {
    if (offset == 0 || offset >= data.size()) {
        return std::nullopt;
    }
    if (unicode) {
        std::size_t end = offset;
        while (end + 1 < data.size() && (data[end] != 0 || data[end + 1] != 0)) {
            end += 2;
        }
        return unitsAt(data, offset, (end - offset) / 2);
    }
    std::size_t end = offset;
    while (end < data.size() && data[end] != 0) {
        end += 1;
    }
    return utf8ToUtf16(latin1ToUtf8(data, offset, end - offset));
}

// A StringData entry: a 2-byte character count, then the characters. Reading moves position past it.
std::u16string readString(const std::vector<std::uint8_t>& data, std::size_t& position, bool unicode) {
    const std::size_t count = u16At(data, position);
    position += 2;
    const std::size_t bytes = unicode ? count * 2 : count;
    if (position > data.size() || data.size() - position < bytes) {
        truncated();
    }
    const std::u16string text = unicode ? unitsAt(data, position, count) : utf8ToUtf16(latin1ToUtf8(data, position, count));
    position += bytes;
    return text;
}

std::size_t skipString(const std::vector<std::uint8_t>& data, std::size_t position, bool unicode) {
    const std::size_t count = u16At(data, position);
    return position + 2 + (unicode ? count * 2 : count);
}

// LinkInfo holds the local base path and a common suffix. The Unicode forms are used when the header has them.
std::optional<std::u16string> linkInfoPath(const std::vector<std::uint8_t>& data, std::size_t start) {
    const std::uint32_t flags = u32At(data, start + 8);
    if ((flags & kVolumeIdAndLocalBasePath) == 0) {
        return std::nullopt;
    }
    const std::uint32_t headerSize = u32At(data, start + 4);
    const bool hasUnicodeOffsets = headerSize >= 0x24;

    std::optional<std::u16string> basePath;
    if (hasUnicodeOffsets) {
        basePath = zeroTerminated(data, start + u32At(data, start + 0x1C), true);
    }
    if (!basePath) {
        basePath = zeroTerminated(data, start + u32At(data, start + 0x10), false);
    }
    std::optional<std::u16string> suffix;
    if (hasUnicodeOffsets) {
        suffix = zeroTerminated(data, start + u32At(data, start + 0x20), true);
    }
    if (!suffix) {
        suffix = zeroTerminated(data, start + u32At(data, start + 0x18), false);
    }

    if (!basePath || basePath->empty()) {
        return std::nullopt;
    }
    if (!suffix || suffix->empty()) {
        return basePath;
    }
    // Joined in Windows form, since this is the path the shortcut's owner stores.
    return *basePath + u"\\" + *suffix;
}

}  // namespace

ShellLinkTarget parseShellLink(const std::vector<std::uint8_t>& data, const std::filesystem::path& shortcutFolder) {
    if (data.size() < kHeaderSize || u32At(data, 0) != kHeaderSize) {
        throw std::runtime_error("Not a Windows shortcut.");
    }
    const std::uint32_t flags = u32At(data, 0x14);
    std::size_t position = kHeaderSize;

    if (flags & kHasLinkTargetIdList) {
        position += 2 + u16At(data, position);
    }

    std::optional<std::u16string> target;
    if (flags & kHasLinkInfo) {
        target = linkInfoPath(data, position);
        position += u32At(data, position);
    }

    const bool unicode = (flags & kIsUnicode) != 0;
    if (flags & kHasName) {
        position = skipString(data, position, unicode);
    }

    std::optional<std::u16string> relative;
    if (flags & kHasRelativePath) {
        relative = readString(data, position, unicode);
    }

    if (flags & kHasWorkingDir) {
        position = skipString(data, position, unicode);
    }

    std::optional<std::u16string> arguments;
    if (flags & kHasArguments) {
        arguments = readString(data, position, unicode);
    }

    // The absolute path from LinkInfo is preferred. The relative path is only a fallback, resolved against the shortcut's folder.
    if (!target && relative) {
        std::u16string replaced = *relative;
        for (char16_t& c : replaced) {
            if (c == u'\\') {
                c = u'/';
            }
        }
        target = pathToUtf16((shortcutFolder / pathFromUtf16(replaced)).lexically_normal());
    }
    return ShellLinkTarget{target, arguments};
}

ShellLinkTarget readShellLink(const std::filesystem::path& path) {
    const auto data = readFileBytes(path);
    if (!data) {
        throw std::runtime_error("The shortcut couldn't be read.");
    }
    return parseShellLink(*data, std::filesystem::absolute(path).parent_path());
}

}  // namespace gwl
