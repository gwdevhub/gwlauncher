#include "LauncherUpdate.h"

#include <cstdlib>
#include <stdexcept>

#include "utils/Crypto.h"
#include "utils/Files.h"
#include "utils/Parsing.h"
#include "utils/Strings.h"
#include "Messages.h"
#include "Process.h"

#ifndef GWL_BUILD
#define GWL_BUILD 0
#endif

namespace gwl {

namespace {

const char* const kReleasesUrl = "https://api.github.com/repos/gwdevhub/gwlauncher/releases";

}  // namespace

std::optional<std::uint32_t> revisionOfTag(const std::string& tag) {
    if (tag.empty() || tag[0] != 'r') {
        return std::nullopt;
    }
    const std::string rest = tag.substr(1);
    const std::string number = rest.substr(0, rest.find('.'));
    if (number.size() > 9) {
        return std::nullopt;
    }
    return stringToUint32(number);
}

std::optional<LauncherRelease> releaseFromJson(const std::string& releasesJson) {
    const Json releases = Json::parse(releasesJson, nullptr, false);
    if (releases.is_discarded() || !releases.is_array()) {
        throw std::runtime_error("GitHub returned no release list.");
    }
    const char* const asset = launcherAssetName();
    for (const Json& release : releases) {
        if (release.value("draft", false) || release.value("prerelease", false)) {
            continue;
        }
        const auto revision = release.contains("tag_name") && release["tag_name"].is_string() ? revisionOfTag(release["tag_name"])
                                                                                               : std::nullopt;
        if (!revision) {
            return std::nullopt;
        }
        LauncherRelease result;
        result.revision = *revision;
        result.notes = release.value("body", std::string());
        result.url = release.value("html_url", std::string());
        // A platform that updates from its release page needs no asset, so only the release's metadata is read.
        if (!asset) {
            return result;
        }
        if (!release.contains("assets") || !release["assets"].is_array()) {
            return std::nullopt;
        }
        const Json* found = nullptr;
        for (const Json& item : release["assets"]) {
            if (item.value("name", std::string()) == asset) {
                found = &item;
                break;
            }
        }
        if (!found) {
            return std::nullopt;
        }
        const std::string digest = found->value("digest", std::string());
        const std::string prefix = "sha256:";
        if (digest.compare(0, prefix.size(), prefix) != 0) {
            throw std::runtime_error("GitHub gave no checksum for the update.");
        }
        result.download = found->value("browser_download_url", std::string());
        result.sha256 = digest.substr(prefix.size());
        if (result.download.empty()) {
            throw std::runtime_error("GitHub gave no download for the update.");
        }
        return result;
    }
    return std::nullopt;
}

std::optional<LauncherRelease> latestLauncherRelease() {
    const char* override = std::getenv("GWLAUNCHER_RELEASES_URL");
    const std::string url = override && *override ? override : kReleasesUrl;
    const auto dest = std::filesystem::temp_directory_path() / "gwl-launcher-releases.json";
    if (runProgram({"curl", "-sfL", "-H", "User-Agent: GWLauncher", "-H", "Accept: application/vnd.github+json", "-o", pathToUtf8(dest), url}) != 0) {
        throw std::runtime_error("GitHub couldn't be reached.");
    }
    const std::string text = textOf(readFileBytes(dest).value_or(Bytes()));
    std::error_code error;
    std::filesystem::remove(dest, error);
    return releaseFromJson(text);
}

std::uint32_t currentBuild() {
    return static_cast<std::uint32_t>(GWL_BUILD);
}

void downloadLauncherRelease(const LauncherRelease& release, const std::filesystem::path& dest, const std::atomic<bool>& cancel,
                             const ProgressReport& report) {
    if (cancel.load()) {
        throw std::runtime_error(kCancelledMessage);
    }
    report("Downloading the update...", 0.0);
    // The download is one curl call, so it can't be cancelled part way through, as the Rust launcher's chunked download can.
    if (runProgram({"curl", "-sfL", "-o", pathToUtf8(dest), release.download}) != 0) {
        std::error_code error;
        std::filesystem::remove(dest, error);
        throw std::runtime_error("The update couldn't be downloaded.");
    }
    if (cancel.load()) {
        std::error_code error;
        std::filesystem::remove(dest, error);
        throw std::runtime_error(kCancelledMessage);
    }
    report("Checking the update...", 0.9);
    const std::string digest = sha256Hex(readFileBytes(dest).value_or(Bytes()));
    if (lowerCase(digest) != lowerCase(release.sha256)) {
        std::error_code error;
        std::filesystem::remove(dest, error);
        throw std::runtime_error("The download didn't match GitHub's checksum.");
    }
}

}  // namespace gwl
