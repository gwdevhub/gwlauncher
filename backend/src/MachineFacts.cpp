#include "MachineFacts.h"

#include "GMod.h"
#include "Launch.h"

namespace gwl {

MachineFacts measureMachine() {
    MachineFacts facts;
    facts.steamClientInstalled = steamClientInstalled();
    facts.protonInstalled = facts.steamClientInstalled && protonInstalled();
    facts.wineRuns = wineRuns();
    facts.wineInstall = wineInstallCommand();
    return facts;
}

}  // namespace gwl
