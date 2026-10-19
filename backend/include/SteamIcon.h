// The Steam icon Steam accounts show: Steam's favicon, fetched once and kept in the launcher's data folder. Until it's there, and
// when it can't be fetched, the account shows the helmet.

#pragma once

#include <functional>
#include <optional>
#include <string>

namespace gwl {

// The icon as a PNG data URL, once it's in the data folder. Nothing before that.
std::optional<std::string> steamIconUrl();

// Fetches the icon in the background when it isn't in the data folder yet, then calls done on the window's thread. A failed fetch is
// silent, and the next launch tries again.
void fetchSteamIcon(const std::function<void()>& done);

}  // namespace gwl
