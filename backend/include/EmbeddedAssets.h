#pragma once

#include <string>

namespace gwl {

// Files compiled into the executable, generated from the frontend/ and assets/ folders at build time.
// The page as the executable holds it, gzip-compressed.
std::string embeddedPageGzip();
// The page, decompressed.
std::string embeddedPage();
std::string embeddedHelmetPng();
// The template the stylesheet override file is written from.
std::string embeddedOverrideTemplate();

}  // namespace gwl
