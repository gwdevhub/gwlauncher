// The user's stylesheet overrides: GWLauncher.override.css, in the folder the launcher runs from. Its rules go after the page's
// built-in styles, so they win over them. The same file and rules as the Rust launcher.

#pragma once

#include <filesystem>
#include <string>

namespace gwl {

// The override file's text. The file is written from the template the first time, when it's missing. Empty when the file
// can't be read, so there are no overrides.
std::string overrideCss(const std::filesystem::path& dir, const std::string& overrideTemplate);

// The page with the overrides in a style element just before </head>. The page is unchanged when there are none.
std::string pageWithOverrides(const std::string& page, const std::string& overrides);

}  // namespace gwl
