#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace gwl {

// Checked conversions return nothing when the input is invalid or out of range.
std::optional<std::uint32_t> stringToUint32(const std::string& text);
std::optional<std::size_t> stringToSize(const std::string& text);
// Decimal floating-point text, parsed at double precision; rejects non-finite and partially parsed values.
std::optional<double> stringToFloat(const std::string& text);
std::optional<std::uint32_t> intToUint32(std::int64_t number);
// Rounds nonnegative finite values to the nearest integer, with halves rounded up.
std::optional<std::uint32_t> floatToUint32(double number);

bool hasArgument(const std::vector<std::string>& arguments, const std::string& name, std::size_t first = 0);
// Reads the token after the first matching argument; returns nothing when absent, missing or empty.
std::optional<std::string> parseArgumentWithValue(const std::vector<std::string>& arguments, const std::string& name,
                                                std::size_t first = 0);

}  // namespace gwl
