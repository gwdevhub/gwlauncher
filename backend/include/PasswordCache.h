// The master password remembered on this computer, as its hash, so the accounts open without asking. Each platform keeps it
// where only this user can read it, in the form the Rust launcher uses: a file on Linux, and on Windows a DPAPI-protected value
// in the user's registry. Implemented in platform/<os>/PasswordCache.cpp.

#pragma once

#include <optional>

#include "AccountFile.h"

namespace gwl {

std::optional<PasswordHash> rememberedPassword();
void rememberPassword(const PasswordHash& password);
void forgetPassword();

}  // namespace gwl
