#include "GwPe.h"

#include <cstddef>
#include <cstring>
#include <map>
#include <optional>
#include <string>
#include <utility>
#include <vector>

#include "utils/Bytes.h"

namespace gwl {

namespace {

struct Section {
    std::string name;
    std::uint32_t virtualSize = 0;
    std::uint32_t virtualAddress = 0;
    std::uint32_t pointerToRawData = 0;
};

// PE32 headers, with PE32+ accepted only for icon resource extraction.
class Image {
public:
    static std::optional<Image> parse(const std::vector<std::uint8_t>& data, bool allowPe32Plus = false) {
        Image image(data);
        const auto pe = image.u32(0x3C);
        if (!pe || !image.bytesAt(*pe, 4) || std::memcmp(data.data() + *pe, "PE\0\0", 4) != 0) {
            return std::nullopt;
        }
        const auto sectionCount = image.u16(*pe + 6);
        const std::size_t optional = *pe + 24;
        const auto magic = image.u16(optional);
        if (!sectionCount || !magic || (*magic != 0x10B && !(allowPe32Plus && *magic == 0x20B))) {
            return std::nullopt;
        }
        image.imageBase = *magic == 0x10B ? image.u32(optional + 28).value_or(0) : 0;
        // PE32+ moves the data directories from offset 96 to 112; resources are the third directory.
        image.resources = image.u32(optional + (*magic == 0x20B ? 112 : 96) + 2 * 8).value_or(0);
        const auto sizeOfOptional = image.u16(*pe + 20);
        if (!sizeOfOptional) {
            return std::nullopt;
        }
        const std::size_t firstSection = optional + *sizeOfOptional;
        for (std::size_t i = 0; i < *sectionCount; ++i) {
            const std::size_t at = firstSection + i * 40;
            const auto name = image.bytesAt(at, 8);
            const auto virtualSize = image.u32(at + 8);
            const auto virtualAddress = image.u32(at + 12);
            const auto pointer = image.u32(at + 20);
            if (!name || !virtualSize || !virtualAddress || !pointer) {
                return std::nullopt;
            }
            // The name is eight bytes, padded with NULs.
            Section section;
            for (std::size_t j = 0; j < 8 && name[j] != 0; ++j) {
                section.name.push_back(static_cast<char>(name[j]));
            }
            section.virtualSize = *virtualSize;
            section.virtualAddress = *virtualAddress;
            section.pointerToRawData = *pointer;
            image.sections.push_back(section);
        }
        return image;
    }

    const Section* section(const std::string& name) const {
        for (const Section& section : sections) {
            if (section.name == name) {
                return &section;
            }
        }
        return nullptr;
    }

    // The file offset of an address, if it's inside a section. The section's end address counts as inside, as in the C# parser.
    // Searches from start to end, forwards when end is after start and backwards otherwise. Returns the address of the match plus
    // offset, or nothing.
    std::optional<std::uint32_t> find(const std::vector<std::uint8_t>& pattern, const std::string& mask, std::uint32_t extra,
                                      std::uint32_t start, std::uint32_t end) const {
        if (end >= start) {
            const auto from = offset(start);
            const auto endOffset = offset(end);
            if (!from || !endOffset || *endOffset < pattern.size()) {
                return std::nullopt;
            }
            const std::size_t to = *endOffset - pattern.size();
            for (std::size_t at = *from; at <= to && at >= *from; ++at) {
                if (matches(at, pattern, mask)) {
                    return start + static_cast<std::uint32_t>(at - *from) + extra;
                }
            }
            return std::nullopt;
        }
        const auto startOffset = offset(start);
        const auto to = offset(end);
        if (!startOffset || !to || *startOffset < pattern.size()) {
            return std::nullopt;
        }
        const std::size_t from = *startOffset - pattern.size();
        for (std::size_t at = from + 1; at-- > *to;) {
            if (matches(at, pattern, mask)) {
                return end + static_cast<std::uint32_t>(at - *to) + extra;
            }
        }
        return std::nullopt;
    }

    std::optional<std::uint32_t> readU32(std::uint32_t rva) const {
        const auto at = offset(rva);
        return at ? u32(*at) : std::nullopt;
    }

    std::uint32_t imageBase = 0;

    // Every resource of one type as (id, bytes), using the first language of each. Nothing when the type is missing.
    std::optional<std::vector<std::pair<std::uint32_t, Bytes>>> resourcesOfType(std::uint32_t typeId) const {
        if (resources == 0) {
            return std::nullopt;
        }
        const auto root = directory(resources);
        if (!root) {
            return std::nullopt;
        }
        std::optional<std::uint32_t> names;
        for (const DirectoryEntry& entry : *root) {
            if (entry.id == typeId && entry.isDirectory) {
                names = entry.target;
            }
        }
        if (!names) {
            return std::nullopt;
        }
        const auto byName = directory(resources + *names);
        if (!byName) {
            return std::nullopt;
        }
        std::vector<std::pair<std::uint32_t, Bytes>> found;
        for (const DirectoryEntry& entry : *byName) {
            if (!entry.isDirectory) {
                continue;
            }
            const auto languages = directory(resources + entry.target);
            if (!languages || languages->empty()) {
                return std::nullopt;
            }
            const DirectoryEntry& first = languages->front();
            if (first.isDirectory) {
                continue;
            }
            auto data = resourceData(resources + first.target);
            if (!data) {
                return std::nullopt;
            }
            found.emplace_back(entry.id, std::move(*data));
        }
        return found;
    }

private:
    struct DirectoryEntry {
        std::uint32_t id;
        bool isDirectory;
        std::uint32_t target;
    };

    // The entries of a resource directory. Named and numbered entries come alike.
    std::optional<std::vector<DirectoryEntry>> directory(std::uint32_t rva) const {
        const auto at = offset(rva);
        if (!at) {
            return std::nullopt;
        }
        const auto named = u16(*at + 12);
        const auto ids = u16(*at + 14);
        if (!named || !ids) {
            return std::nullopt;
        }
        std::vector<DirectoryEntry> entries;
        for (std::size_t i = 0; i < *named + *ids; ++i) {
            const auto id = u32(*at + 16 + i * 8);
            const auto target = u32(*at + 16 + i * 8 + 4);
            if (!id || !target) {
                return std::nullopt;
            }
            entries.push_back({*id & 0x7FFFFFFF, (*target & 0x80000000) != 0, *target & 0x7FFFFFFF});
        }
        return entries;
    }

    // The bytes a data entry points to. The entry holds the address and size of the data.
    std::optional<Bytes> resourceData(std::uint32_t entryRva) const {
        const auto at = offset(entryRva);
        if (!at) {
            return std::nullopt;
        }
        const auto dataRva = u32(*at);
        const auto size = u32(*at + 4);
        if (!dataRva || !size) {
            return std::nullopt;
        }
        const auto start = offset(*dataRva);
        if (!start || *start > data_.size() || *size > data_.size() - *start) {
            return std::nullopt;
        }
        return Bytes(data_.begin() + static_cast<std::ptrdiff_t>(*start), data_.begin() + static_cast<std::ptrdiff_t>(*start + *size));
    }

    std::optional<std::size_t> offset(std::uint32_t rva) const {
        for (const Section& section : sections) {
            if (rva >= section.virtualAddress && rva <= section.virtualAddress + section.virtualSize) {
                return static_cast<std::size_t>(rva - section.virtualAddress + section.pointerToRawData);
            }
        }
        return std::nullopt;
    }

    explicit Image(const std::vector<std::uint8_t>& data) : data_(data) {}

    bool matches(std::size_t at, const std::vector<std::uint8_t>& pattern, const std::string& mask) const {
        for (std::size_t j = 0; j < pattern.size(); ++j) {
            if (mask[j] == 'x' && (at + j >= data_.size() || data_[at + j] != pattern[j])) {
                return false;
            }
        }
        return true;
    }

    const std::uint8_t* bytesAt(std::size_t at, std::size_t count) const {
        if (at > data_.size() || count > data_.size() - at) {
            return nullptr;
        }
        return data_.data() + at;
    }

    std::optional<std::uint16_t> u16(std::size_t at) const {
        const std::uint8_t* bytes = bytesAt(at, 2);
        return bytes ? std::optional<std::uint16_t>(static_cast<std::uint16_t>(bytes[0] | bytes[1] << 8)) : std::nullopt;
    }

    std::optional<std::uint32_t> u32(std::size_t at) const {
        const std::uint8_t* bytes = bytesAt(at, 4);
        if (!bytes) {
            return std::nullopt;
        }
        return static_cast<std::uint32_t>(bytes[0]) | static_cast<std::uint32_t>(bytes[1]) << 8 |
               static_cast<std::uint32_t>(bytes[2]) << 16 | static_cast<std::uint32_t>(bytes[3]) << 24;
    }

    const std::vector<std::uint8_t>& data_;
    std::vector<Section> sections;
    std::uint32_t resources = 0;
};

}  // namespace

bool isGwExecutable(const std::vector<std::uint8_t>& data) {
    return Image::parse(data).has_value();
}

std::optional<std::int32_t> gwFileId(const std::vector<std::uint8_t>& data) {
    const auto image = Image::parse(data);
    if (!image) {
        return std::nullopt;
    }
    const Section* text = image->section(".text");
    const Section* rdata = image->section(".rdata");
    if (!text || !rdata) {
        return std::nullopt;
    }

    const std::string assertion = "index < arrsize(s_eula)";
    const std::vector<std::uint8_t> assertionBytes(assertion.begin(), assertion.end());
    const auto assertionRva = image->find(assertionBytes, std::string(assertion.size(), 'x'), 0, rdata->virtualAddress,
                                          rdata->virtualAddress + rdata->virtualSize);
    if (!assertionRva) {
        return std::nullopt;
    }

    // A push of the assertion's address followed by a call: B9 imm32 E8.
    std::vector<std::uint8_t> usePattern{0xB9};
    const std::uint32_t address = image->imageBase + *assertionRva;
    for (int i = 0; i < 4; ++i) {
        usePattern.push_back(static_cast<std::uint8_t>(address >> (8 * i)));
    }
    usePattern.push_back(0xE8);
    const auto useRva = image->find(usePattern, "xxxxxx", 0, text->virtualAddress, text->virtualAddress + text->virtualSize);
    if (!useRva) {
        return std::nullopt;
    }

    // The function that returns the id is a MOV eax, imm32 followed by RET, found just before the assertion's use.
    const auto functionRva = image->find({0xB8, 0, 0, 0, 0, 0xC3}, "x????x", 0, *useRva, *useRva - 0xFF);
    if (!functionRva) {
        return std::nullopt;
    }
    const auto id = image->readU32(*functionRva + 1);
    if (!id) {
        return std::nullopt;
    }
    return static_cast<std::int32_t>(*id);
}


std::optional<Bytes> gwIconFile(const std::vector<std::uint8_t>& data) {
    const std::uint32_t kIcon = 3;
    const std::uint32_t kGroupIcon = 14;
    const auto image = Image::parse(data, true);
    if (!image) {
        return std::nullopt;
    }
    const auto groups = image->resourcesOfType(kGroupIcon);
    const auto icons = image->resourcesOfType(kIcon);
    if (!groups || groups->empty() || !icons) {
        return std::nullopt;
    }
    std::map<std::uint32_t, Bytes> images(icons->begin(), icons->end());

    // Each group entry is 14 bytes and names one image by id; the ICO's own entries are 16 bytes.
    const Bytes& group = groups->front().second;
    if (group.size() < 6) {
        return std::nullopt;
    }
    const std::size_t count = readU16LE(group, 4);
    std::vector<std::size_t> entries;
    for (std::size_t i = 0; i < count; ++i) {
        const std::size_t at = 6 + i * 14;
        if (group.size() < at + 14) {
            return std::nullopt;
        }
        entries.push_back(at);
        if (!images.count(readU16LE(group, at + 12))) {
            return std::nullopt;
        }
    }

    Bytes ico{0, 0, 1, 0};
    appendU16LE(ico, static_cast<std::uint16_t>(count));
    std::uint32_t offset = static_cast<std::uint32_t>(6 + 16 * count);
    for (std::size_t at : entries) {
        ico.insert(ico.end(), group.begin() + static_cast<std::ptrdiff_t>(at), group.begin() + static_cast<std::ptrdiff_t>(at + 8));
        const Bytes& image = images[readU16LE(group, at + 12)];
        appendU32LE(ico, static_cast<std::uint32_t>(image.size()));
        appendU32LE(ico, offset);
        offset += static_cast<std::uint32_t>(image.size());
    }
    for (std::size_t at : entries) {
        const Bytes& image = images[readU16LE(group, at + 12)];
        ico.insert(ico.end(), image.begin(), image.end());
    }
    return ico;
}

}  // namespace gwl
