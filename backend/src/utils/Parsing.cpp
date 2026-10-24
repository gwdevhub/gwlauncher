#include "utils/Parsing.h"

#include <charconv>
#include <cmath>
#include <limits>

namespace gwl {

namespace {

template <typename T>
bool stringToUnsigned(const std::string& text, T* value) {
    if (!value || text.empty()) {
        return false;
    }
    T parsed;
    const auto result = std::from_chars(text.data(), text.data() + text.size(), parsed, 10);
    if (result.ec != std::errc{} || result.ptr != text.data() + text.size()) {
        return false;
    }
    *value = parsed;
    return true;
}

}  // namespace

bool stringToUint32(const std::string& text, std::uint32_t* value) {
    return stringToUnsigned(text, value);
}

bool stringToSize(const std::string& text, std::size_t* value) {
    return stringToUnsigned(text, value);
}

bool stringToFloat(const std::string& text, double* value) {
    if (!value || text.empty()) {
        return false;
    }
    double parsed;
    const auto result = std::from_chars(text.data(), text.data() + text.size(), parsed, std::chars_format::general);
    if (result.ec != std::errc{} || result.ptr != text.data() + text.size() || !std::isfinite(parsed)) {
        return false;
    }
    *value = parsed;
    return true;
}

bool intToUint32(std::int64_t number, std::uint32_t* value) {
    if (!value || number < 0 || number > std::numeric_limits<std::uint32_t>::max()) {
        return false;
    }
    *value = static_cast<std::uint32_t>(number);
    return true;
}

bool floatToUint32(double number, std::uint32_t* value) {
    if (!value || !std::isfinite(number) || number < 0) {
        return false;
    }
    const double rounded = std::round(number);
    if (rounded > std::numeric_limits<std::uint32_t>::max()) {
        return false;
    }
    *value = static_cast<std::uint32_t>(rounded);
    return true;
}

bool hasArgument(const std::vector<std::string>& arguments, const std::string& name, std::size_t first) {
    for (std::size_t i = first; i < arguments.size(); ++i) {
        if (arguments[i] == name) {
            return true;
        }
    }
    return false;
}

bool parseArgumentWithValue(const std::vector<std::string>& arguments, const std::string& name, std::string* value,
                            std::size_t first) {
    if (!value) {
        return false;
    }
    for (std::size_t i = first; i < arguments.size(); ++i) {
        if (arguments[i] == name) {
            if (i + 1 == arguments.size() || arguments[i + 1].empty()) {
                return false;
            }
            *value = arguments[i + 1];
            return true;
        }
    }
    return false;
}

}  // namespace gwl
