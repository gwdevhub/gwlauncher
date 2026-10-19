// What's installed on this computer. It's measured once per check round, so every check of the round reads the same facts
// and nothing is measured again for each account.

#pragma once

#include <optional>
#include <string>

namespace gwl {

struct MachineFacts {
    bool steamClientInstalled = false;
    // Proton in Steam's library. Only measured when the Steam client is installed.
    bool protonInstalled = false;
    bool wineRuns = false;
    // The distro's command to install Wine, when there is one.
    std::optional<std::string> wineInstall;
};

// Measures the machine. Runs programs and reads files, so it belongs on a worker.
MachineFacts measureMachine();

}  // namespace gwl
