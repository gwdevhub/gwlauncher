#pragma once

#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace gwl {

// Checked conversions leave the output unchanged on failure, including a null output pointer.
bool stringToUint32(const std::string& text, std::uint32_t* value);
bool stringToSize(const std::string& text, std::size_t* value);
// Decimal floating-point text, parsed at double precision; rejects non-finite and partially parsed values.
bool stringToFloat(const std::string& text, double* value);
bool intToUint32(std::int64_t number, std::uint32_t* value);
// Rounds nonnegative finite values to the nearest integer, with halves rounded up.
bool floatToUint32(double number, std::uint32_t* value);

bool hasArgument(const std::vector<std::string>& arguments, const std::string& name, std::size_t first = 0);
// Reads the token after the first matching argument; absent or missing values leave the output unchanged.
bool parseArgumentWithValue(const std::vector<std::string>& arguments, const std::string& name, std::string* value,
                            std::size_t first = 0);

}  // namespace gwl
