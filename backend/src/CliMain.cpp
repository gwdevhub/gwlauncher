// The command-line launcher, GW_Launcher.cli.exe. The GUI launchers carry it and run it to start a game.

#include <exception>
#include <iostream>
#include <string>
#include <vector>

#include "Cli.h"

int main() {
    try {
        const auto arguments = gwl::commandLineArguments();
        return gwl::runCli(std::vector<std::u16string>(arguments.begin() + 1, arguments.end()));
    } catch (const std::exception& error) {
        std::cerr << error.what() << std::endl;
        return 1;
    }
}
