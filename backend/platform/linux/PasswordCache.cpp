#include "PasswordCache.h"

#include <fcntl.h>
#include <sys/stat.h>
#include <unistd.h>

#include <algorithm>
#include <array>
#include <cstddef>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>

#include "utils/Files.h"
#include "utils/Strings.h"

namespace gwl {

namespace {

// The Rust launcher's file, in its working folder.
const char* const kRememberedFile = "remembered-password";

}  // namespace

std::optional<PasswordHash> rememberedPassword() {
    const auto raw = readFileBytes(kRememberedFile);
    if (!raw) {
        return std::nullopt;
    }
    const std::string text = textOf(*raw);
    const auto bytes = base64Decode(text);
    if (!bytes || bytes->size() != std::tuple_size_v<PasswordHash>) {
        return std::nullopt;
    }
    PasswordHash hash{};
    std::copy(bytes->begin(), bytes->end(), hash.begin());
    return hash;
}

void rememberPassword(const PasswordHash& password) {
    // Created owner-only, so no one else can read the hash while it's written.
    const int fd = open(kRememberedFile, O_WRONLY | O_CREAT | O_TRUNC, 0600);
    if (fd < 0) {
        return;
    }
    fchmod(fd, 0600);
    const std::string text = base64Encode(Bytes(password.begin(), password.end()));
    [[maybe_unused]] const ssize_t written = write(fd, text.data(), text.size());
    close(fd);
}

void forgetPassword() {
    std::error_code error;
    std::filesystem::remove(kRememberedFile, error);
}

}  // namespace gwl
