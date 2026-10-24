#include "utils/Parsing.h"

#include <charconv>
#include <cmath>
#include <limits>

namespace gwl {

namespace {

template <typename T>
std::optional<T> stringToUnsigned(const std::string& text) {
    if (text.empty()) {
        return std::nullopt;
    }
    T parsed;
    const auto result = std::from_chars(text.data(), text.data() + text.size(), parsed, 10);
    if (result.ec != std::errc{} || result.ptr != text.data() + text.size()) {
        return std::nullopt;
    }
    return parsed;
}

}  // namespace

std::optional<std::uint32_t> stringToUint32(const std::string& text) {
    return stringToUnsigned<std::uint32_t>(text);
}

std::optional<std::size_t> stringToSize(const std::string& text) {
    return stringToUnsigned<std::size_t>(text);
}

std::optional<double> stringToFloat(const std::string& text) {
    if (text.empty()) {
        return std::nullopt;
    }
    double parsed;
    const auto result = std::from_chars(text.data(), text.data() + text.size(), parsed, std::chars_format::general);
    if (result.ec != std::errc{} || result.ptr != text.data() + text.size() || !std::isfinite(parsed)) {
        return std::nullopt;
    }
    return parsed;
}

std::optional<std::uint32_t> intToUint32(std::int64_t number) {
    if (number < 0 || number > std::numeric_limits<std::uint32_t>::max()) {
        return std::nullopt;
    }
    return static_cast<std::uint32_t>(number);
}

std::optional<std::uint32_t> floatToUint32(double number) {
    if (!std::isfinite(number) || number < 0) {
        return std::nullopt;
    }
    const double rounded = std::round(number);
    if (rounded > std::numeric_limits<std::uint32_t>::max()) {
        return std::nullopt;
    }
    return static_cast<std::uint32_t>(rounded);
}

bool hasArgument(const std::vector<std::string>& arguments, const std::string& name, std::size_t first) {
    for (std::size_t i = first; i < arguments.size(); ++i) {
        if (arguments[i] == name) {
            return true;
        }
    }
    return false;
}

std::optional<std::string> parseArgumentWithValue(const std::vector<std::string>& arguments, const std::string& name,
                                                std::size_t first) {
    for (std::size_t i = first; i < arguments.size(); ++i) {
        if (arguments[i] == name) {
            if (i + 1 == arguments.size() || arguments[i + 1].empty()) {
                return std::nullopt;
            }
            return arguments[i + 1];
        }
    }
    return std::nullopt;
}

}  // namespace gwl
