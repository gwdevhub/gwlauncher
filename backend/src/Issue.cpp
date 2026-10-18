#include "Issue.h"

namespace gwl {

Json Issue::toJson() const {
    Json json = {
        {"severity", severity()},
        {"message", message()},
        {"wofPath", nullptr},
        {"canDownloadGMod", false},
        {"canUpdateGwExe", false},
        {"canInstallSteam", false},
        {"canInstallProton", false},
        {"canInstallWine", false},
    };
    describe(json);
    return json;
}

}  // namespace gwl
