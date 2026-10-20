// Reading and writing whole files, and the file parts the launcher compares.

#pragma once

#include <filesystem>
#include <optional>
#include <string>

#include "utils/Strings.h"

namespace gwl {

// UI, JSON and process arguments use UTF-8; filesystem operations use native paths.
std::filesystem::path pathFromUtf8(const std::string& text);
std::string pathToUtf8(const std::filesystem::path& path);

// The file's bytes, or nothing when it can't be read.
std::optional<Bytes> readFileBytes(const std::filesystem::path& path);

// Writes the bytes, replacing the file. Returns false when the file can't be written.
bool writeFileBytes(const std::filesystem::path& path, const Bytes& bytes);

// Whether a file can be created in the folder, by writing and removing a probe file. A folder that doesn't exist yet is judged by the
// nearest folder that does, since launching creates it.
bool isWritableFolder(const std::filesystem::path& folder);

// The extension in lower case, with its dot. Empty when the file has none.
std::string lowerExtension(const std::filesystem::path& path);

}  // namespace gwl
