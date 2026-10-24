// String and byte helpers the launcher shares: trimming and case, hex and base64, UTF-8 and UTF-16 text, and byte-order marks.

#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace gwl {

using Bytes = std::vector<std::uint8_t>;

// The text without spaces, tabs or line breaks at either end.
std::string trimmed(const std::string& text);

// The text in lower case, for ASCII letters.
std::string lowerCase(std::string text);

// Seconds without trailing decimal zeroes, preserving millisecond precision.
std::string millisecondsAsSeconds(std::uint32_t milliseconds);

// The bytes as lower-case hex.
std::string hexEncode(const Bytes& bytes);

// Standard base64, with padding, as the Rust and C# launchers store the remembered password and the data URLs use.
std::string base64Encode(const Bytes& bytes);

// Nothing when the text has a symbol outside the alphabet. Padding is skipped.
std::optional<Bytes> base64Decode(const std::string& text);

// The text's bytes, and the bytes read as text without any decoding.
Bytes bytesOf(const std::string& text);
std::string textOf(const Bytes& bytes);

// The text without its UTF-8 byte-order mark, when it starts with one.
std::string withoutByteOrderMark(std::string text);

// Appends the code point as UTF-8.
void appendUtf8(std::string& text, std::uint32_t codePoint);

// UTF-16 code units as UTF-8. Unpaired surrogates become U+FFFD, as Rust's from_utf16_lossy does.
std::string utf16ToUtf8(const std::vector<std::uint16_t>& units);

// Single-byte text as UTF-8, where each byte is a code point of its own, as the C# code's ANSI paths read.
std::string latin1ToUtf8(const Bytes& data, std::size_t offset, std::size_t count);

// UTF-8 decoded with each invalid run replaced by U+FFFD, as Rust's from_utf8_lossy does. invalid is set when there was one.
std::string decodeUtf8Lossy(const Bytes& bytes, bool& invalid);

// The text, or nothing when the bytes aren't valid UTF-8, as Rust's from_utf8 does.
std::optional<std::string> decodeUtf8Strict(const Bytes& bytes);

}  // namespace gwl
