// The command-line launcher, GW_Launcher.cli.exe. The GUI launchers carry it and run it to start a game.

#include <string>
#include <vector>

#include "Cli.h"

int main(int argc, char** argv) {
    return gwl::runCli(std::vector<std::string>(argv + 1, argv + argc));
}
