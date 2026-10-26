#include "utils/Strings.h"

#include <algorithm>
#include <charconv>
#include <cctype>
#include <cstddef>

namespace gwl {

namespace {

const char* const kAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

static_assert(sizeof(wchar_t) == 2 || sizeof(wchar_t) == 4, "Wide text requires 16-bit or 32-bit wchar_t");

template <typename Input, typename Append>
bool decodeUtf8(const Input& bytes, Append append) {
    bool invalid = false;
    std::size_t i = 0;
    while (i < bytes.size()) {
        const std::uint8_t lead = static_cast<std::uint8_t>(bytes[i]);
        if (lead < 0x80) {
            append(lead);
            i += 1;
            continue;
        }
        std::size_t length = 0;
        std::uint8_t low = 0x80;
        std::uint8_t high = 0xBF;
        if (lead >= 0xC2 && lead <= 0xDF) {
            length = 2;
        } else if (lead >= 0xE0 && lead <= 0xEF) {
            length = 3;
            low = lead == 0xE0 ? 0xA0 : 0x80;
            high = lead == 0xED ? 0x9F : 0xBF;
        } else if (lead >= 0xF0 && lead <= 0xF4) {
            length = 4;
            low = lead == 0xF0 ? 0x90 : 0x80;
            high = lead == 0xF4 ? 0x8F : 0xBF;
        } else {
            invalid = true;
            append(0xFFFD);
            i += 1;
            continue;
        }
        std::uint32_t codePoint = lead & (0x7F >> length);
        // Each continuation byte must be in range; the first bad one ends the sequence, which is then replaced once.
        std::size_t next = i + 1;
        bool valid = true;
        for (std::size_t k = 1; k < length; ++k, ++next) {
            if (next >= bytes.size()) {
                valid = false;
                break;
            }
            const std::uint8_t continuation = static_cast<std::uint8_t>(bytes[next]);
            const std::uint8_t lowBound = k == 1 ? low : 0x80;
            const std::uint8_t highBound = k == 1 ? high : 0xBF;
            if (continuation < lowBound || continuation > highBound) {
                valid = false;
                break;
            }
            codePoint = (codePoint << 6) | (continuation & 0x3F);
        }
        if (!valid) {
            invalid = true;
            append(0xFFFD);
        } else {
            append(codePoint);
        }
        i = next;
    }
    return invalid;
}

}  // namespace

std::string trimmed(const std::string& text) {
    const std::size_t first = text.find_first_not_of(" \t\r\n");
    if (first == std::string::npos) {
        return "";
    }
    const std::size_t last = text.find_last_not_of(" \t\r\n");
    return text.substr(first, last - first + 1);
}

std::string lowerCase(std::string text) {
    for (char& c : text) {
        c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    }
    return text;
}

std::optional<std::uint32_t> stringToUint32(const std::string& text) {
    std::uint32_t value;
    const auto [end, error] = std::from_chars(text.data(), text.data() + text.size(), value);
    if (error != std::errc{} || end != text.data() + text.size()) {
        return std::nullopt;
    }
    return value;
}

std::optional<std::string> parseArgumentWithValue(const std::vector<std::string>& arguments, const std::string& name,
                                                std::size_t first) {
    if (first >= arguments.size()) {
        return std::nullopt;
    }
    const auto found = std::find(arguments.begin() + first, arguments.end(), name);
    if (found == arguments.end() || found + 1 == arguments.end() || (found + 1)->empty()) {
        return std::nullopt;
    }
    return *(found + 1);
}

std::string hexEncode(const Bytes& bytes) {
    static const char* const digits = "0123456789abcdef";
    std::string text;
    text.reserve(bytes.size() * 2);
    for (std::uint8_t byte : bytes) {
        text += digits[byte >> 4];
        text += digits[byte & 15];
    }
    return text;
}

std::string base64Encode(const Bytes& bytes) {
    std::string encoded;
    encoded.reserve((bytes.size() + 2) / 3 * 4);
    for (std::size_t i = 0; i < bytes.size(); i += 3) {
        const unsigned chunk = (static_cast<unsigned>(bytes[i]) << 16) |
                               (i + 1 < bytes.size() ? static_cast<unsigned>(bytes[i + 1]) << 8 : 0) |
                               (i + 2 < bytes.size() ? static_cast<unsigned>(bytes[i + 2]) : 0);
        encoded += kAlphabet[(chunk >> 18) & 63];
        encoded += kAlphabet[(chunk >> 12) & 63];
        encoded += i + 1 < bytes.size() ? kAlphabet[(chunk >> 6) & 63] : '=';
        encoded += i + 2 < bytes.size() ? kAlphabet[chunk & 63] : '=';
    }
    return encoded;
}

std::optional<Bytes> base64Decode(const std::string& text) {
    Bytes bytes;
    unsigned buffer = 0;
    int bits = 0;
    for (char symbol : text) {
        if (symbol == '=') {
            continue;
        }
        const char* found = nullptr;
        for (const char* letter = kAlphabet; *letter; ++letter) {
            if (*letter == symbol) {
                found = letter;
                break;
            }
        }
        if (!found) {
            return std::nullopt;
        }
        buffer = (buffer << 6) | static_cast<unsigned>(found - kAlphabet);
        bits += 6;
        if (bits >= 8) {
            bits -= 8;
            bytes.push_back(static_cast<std::uint8_t>(buffer >> bits));
            buffer &= (1u << bits) - 1;
        }
    }
    return bytes;
}

Bytes bytesOf(const std::string& text) {
    return Bytes(text.begin(), text.end());
}

std::string textOf(const Bytes& bytes) {
    return std::string(bytes.begin(), bytes.end());
}

std::string withoutByteOrderMark(std::string text) {
    if (text.rfind("\xEF\xBB\xBF", 0) == 0) {
        text.erase(0, 3);
    }
    return text;
}

void appendUtf8(std::string& text, std::uint32_t codePoint) {
    if (codePoint < 0x80) {
        text.push_back(static_cast<char>(codePoint));
    } else if (codePoint < 0x800) {
        text.push_back(static_cast<char>(0xC0 | (codePoint >> 6)));
        text.push_back(static_cast<char>(0x80 | (codePoint & 0x3F)));
    } else if (codePoint < 0x10000) {
        text.push_back(static_cast<char>(0xE0 | (codePoint >> 12)));
        text.push_back(static_cast<char>(0x80 | ((codePoint >> 6) & 0x3F)));
        text.push_back(static_cast<char>(0x80 | (codePoint & 0x3F)));
    } else {
        text.push_back(static_cast<char>(0xF0 | (codePoint >> 18)));
        text.push_back(static_cast<char>(0x80 | ((codePoint >> 12) & 0x3F)));
        text.push_back(static_cast<char>(0x80 | ((codePoint >> 6) & 0x3F)));
        text.push_back(static_cast<char>(0x80 | (codePoint & 0x3F)));
    }
}

std::string utf16ToUtf8(const std::vector<std::uint16_t>& units) {
    std::string text;
    for (std::size_t i = 0; i < units.size(); ++i) {
        const std::uint32_t unit = units[i];
        if (unit >= 0xD800 && unit <= 0xDBFF && i + 1 < units.size() && units[i + 1] >= 0xDC00 && units[i + 1] <= 0xDFFF) {
            appendUtf8(text, 0x10000 + ((unit - 0xD800) << 10) + (units[i + 1] - 0xDC00));
            i += 1;
        } else if (unit >= 0xD800 && unit <= 0xDFFF) {
            appendUtf8(text, 0xFFFD);
        } else {
            appendUtf8(text, unit);
        }
    }
    return text;
}

std::wstring toWide(const std::string& text) {
    std::wstring out;
    decodeUtf8(text, [&](std::uint32_t codePoint) {
        if constexpr (sizeof(wchar_t) == 2) {
            if (codePoint > 0xFFFF) {
                codePoint -= 0x10000;
                out.push_back(static_cast<wchar_t>(0xD800 + (codePoint >> 10)));
                out.push_back(static_cast<wchar_t>(0xDC00 + (codePoint & 0x3FF)));
                return;
            }
        }
        out.push_back(static_cast<wchar_t>(codePoint));
    });
    return out;
}

std::string toUtf8(const std::wstring& text) {
    if constexpr (sizeof(wchar_t) == 2) {
        return utf16ToUtf8(std::vector<std::uint16_t>(text.begin(), text.end()));
    } else {
        std::string out;
        for (wchar_t unit : text) {
            const auto codePoint = static_cast<std::uint32_t>(unit);
            const bool invalid = codePoint > 0x10FFFF || (codePoint >= 0xD800 && codePoint <= 0xDFFF);
            appendUtf8(out, invalid ? 0xFFFD : codePoint);
        }
        return out;
    }
}

std::string latin1ToUtf8(const Bytes& data, std::size_t offset, std::size_t count) {
    std::string text;
    for (std::size_t i = 0; i < count; ++i) {
        appendUtf8(text, data[offset + i]);
    }
    return text;
}

std::string decodeUtf8Lossy(const Bytes& bytes, bool& invalid) {
    std::string text;
    invalid = decodeUtf8(bytes, [&](std::uint32_t codePoint) { appendUtf8(text, codePoint); });
    return text;
}

std::optional<std::string> decodeUtf8Strict(const Bytes& bytes) {
    bool invalid = false;
    std::string text = decodeUtf8Lossy(bytes, invalid);
    if (invalid) {
        return std::nullopt;
    }
    return text;
}

}  // namespace gwl
