// Windows game-launch mode; Linux runs the same code in its helper under Wine.

#include <windows.h>
#include <shellapi.h>

#include <algorithm>
#include <cstdint>
#include <filesystem>
#include <functional>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

#include "Accounts.h"
#include "CliGame.h"
#include "Cli.h"
#include "Launch.h"
#include "utils/Files.h"
#include "utils/Strings.h"

namespace gwl {

namespace {

// The usage, with the account file's format. Each account field is as Accounts.json holds it.
const char* const kUsage = R"usage(Usage: GW_Launcher.cli.exe --launch <account.json> <timeout-ms> [--confirm]

  <account.json>  An account, in the form Accounts.json holds one:
                  {
                    "character": "Name",            required: the character's name
                    "email": "user@example.com",    required: the account's email
                    "gwpath": "C:\\...\\Gw.exe",    required: the game's executable, a Windows path
                    "password": "",                 required: the account's password
                    "extraargs": "",                optional: extra command-line arguments for the game
                    "elevated": false,              optional: run the game elevated
                    "is_steam_account": false,      optional: launch through Steam
                    "title": "",                    optional: shown in the list instead of the character
                    "usePluginFolderMods": true,    optional: load the mods in the plugin folder
                    "mods": [                       optional: mods to load, in order
                      { "fileName": "C:\\...\\mod.dll", "type": 1, "active": true }
                    ]
                  }
                  A mod's "type" is 0 for a texture mod or 1 for a DLL, and it's required.
                  "fileName" is the mod's path, and "active" defaults to true.
  <timeout-ms>    How long to wait for each mod DLL, the game's window and input readiness (each), in milliseconds.
  --confirm       Hold the game suspended after it's started. Prints "suspended" on stdout, then
                  reads "resume" or "cancel" from stdin.

Exit codes: 0 when the game started, 1 when the launch failed or its arguments are wrong (the reason is on
stderr), 2 when --launch is missing.
)usage";


int launch(const std::vector<std::string>& arguments) {
    // Guild Wars needs d3d9.dll. It's loaded here the way Guild Wars will load it, so a Wine that can't find or load it stops the launch
    // before anything starts.
    const HMODULE direct3D = LoadLibraryW(L"d3d9.dll");
    if (!direct3D) {
        throw std::runtime_error("Guild Wars needs d3d9.dll, which this Wine can't find or load (Windows error " + std::to_string(GetLastError()) + ").");
    }
    FreeLibrary(direct3D);
    if (arguments.empty()) {
        throw std::runtime_error("--launch needs the account file.");
    }
    if (arguments.size() < 2) {
        throw std::runtime_error("--launch needs the timeout in milliseconds.");
    }
    const auto timeout = stringToUint32(arguments[1]);
    if (!timeout) {
        throw std::runtime_error("--launch needs the timeout in milliseconds.");
    }

    const auto bytes = readFileBytes(pathFromUtf8(arguments[0]));
    if (!bytes) {
        throw std::runtime_error("Could not read the launch file: " + arguments[0]);
    }
    Account account;
    try {
        account = Account::fromFileJson(Json::parse(textOf(*bytes)));
    } catch (const std::exception& error) {
        throw std::runtime_error(std::string("Could not read the account: ") + error.what());
    }

    // The game reads its install from the registry, so it's written before each start. A failure doesn't stop the launch.
    try {
        setDefaultGame(pathFromUtf8(account.gwpath));
    } catch (const std::runtime_error&) {
    }

    std::function<bool()> confirm;
    if (std::find(arguments.begin(), arguments.end(), "--confirm") != arguments.end()) {
        confirm = [] {
            std::cout << "suspended" << std::endl;
            std::string answer;
            return static_cast<bool>(std::getline(std::cin, answer)) && trimmed(answer) == "resume";
        };
    }
    // The Linux launcher checks for Steam before it starts this, because Wine can't see the Linux Steam client's processes.
    launchGameInProcess(account, pathToUtf8(std::filesystem::current_path()), *timeout, confirm, false);
    return 0;
}

}  // namespace

std::vector<std::string> commandLineArguments() {
    int count = 0;
    wchar_t** wide = CommandLineToArgvW(GetCommandLineW(), &count);
    if (!wide) {
        throw std::runtime_error("The command line couldn't be read.");
    }
    std::vector<std::string> arguments;
    try {
        for (int i = 0; i < count; ++i) {
            const std::wstring text(wide[i]);
            arguments.push_back(utf16ToUtf8(std::vector<std::uint16_t>(text.begin(), text.end())));
        }
    } catch (...) {
        LocalFree(wide);
        throw;
    }
    LocalFree(wide);
    return arguments;
}

int runCli(const std::vector<std::string>& arguments) {
    const auto found = std::find(arguments.begin(), arguments.end(), "--launch");
    if (found == arguments.end()) {
        std::cerr << kUsage << std::endl;
        return 2;
    }
    try {
        return launch(std::vector<std::string>(found + 1, arguments.end()));
    } catch (const std::exception& error) {
        std::cerr << error.what() << std::endl;
        return 1;
    }
}

}  // namespace gwl
