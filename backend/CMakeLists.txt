cmake_minimum_required(VERSION 3.20)
# A compiler cache, when one is installed, reuses earlier compiles of unchanged sources. It's set before project(), which reads it.
find_program(GWL_CCACHE ccache)
if(GWL_CCACHE)
    set(CMAKE_CXX_COMPILER_LAUNCHER "${GWL_CCACHE}")
endif()
project(gwl_backend CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
# CLion and clangd read the compile commands to know each file's flags, include folders and defines.
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# GWL_CLI_ONLY builds the Windows helper that the Linux launcher embeds and runs under Wine.
option(GWL_CLI_ONLY "Build only the Windows command-line launcher" OFF)

# Built executables go to the repository's bin folder: bin/windows for the Windows build, and bin/linux/release or bin/linux/debug
# for Linux, by build type, so the two never mix.
if(GWL_CLI_ONLY)
    set(GWL_BIN_DIR "${CMAKE_CURRENT_BINARY_DIR}")
elseif(WIN32)
    set(GWL_BIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../bin/windows")
else()
    if(CMAKE_BUILD_TYPE STREQUAL "Release")
        set(GWL_BIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../bin/linux/release")
    else()
        set(GWL_BIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../bin/linux/debug")
    endif()
endif()
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${GWL_BIN_DIR}")


# The WebView2 loader, vendored in third_party/webview2. The Windows launcher carries it as a resource.
set(GWL_WEBVIEW2_LOADER "${CMAKE_CURRENT_SOURCE_DIR}/third_party/webview2/bin/x64/WebView2Loader.dll")

# The platform's sources.
add_library(gwl_core STATIC src/Messages.cpp src/WebViewWindow.cpp src/Settings.cpp src/Accounts.cpp src/DataUrl.cpp src/tray/TrayRegion.cpp src/GMod.cpp src/WorkerPool.cpp src/MachineFacts.cpp src/IssueChecker.cpp src/Instance.cpp src/Stylesheets.cpp src/EmbeddedAssets.cpp src/utils/Strings.cpp src/utils/Files.cpp src/utils/Crypto.cpp src/AccountFile.cpp src/ShellLink.cpp src/ModFiles.cpp src/LauncherUpdate.cpp src/GameLaunch.cpp src/CliInstall.cpp src/Steam.cpp src/Modlist.cpp src/GwNet.cpp src/GwHuffman.cpp src/GwPe.cpp src/GwInstall.cpp src/WofDecode.cpp src/Wof.cpp src/utils/Bytes.cpp src/utils/Crc.cpp src/utils/Images.cpp src/utils/Inflate.cpp src/GwIcon.cpp src/Issue.cpp src/issues/GwExeMissingIssue.cpp src/issues/SteamNotInstalledIssue.cpp src/issues/ProtonMissingIssue.cpp src/issues/WineMissingIssue.cpp src/issues/GModMissingIssue.cpp src/issues/ModMissingIssue.cpp src/issues/InvalidModIssue.cpp src/issues/InvalidTexmodIssue.cpp src/issues/WofIssue.cpp src/issues/GwExeOutdatedIssue.cpp src/issues/LaunchFoldersIssue.cpp src/issues/InUseIssue.cpp src/Starting.cpp src/SteamIcon.cpp src/TempFiles.cpp)
# The commit count this build is made from, which the launcher checks for updates against. Zero without git history.
# A container build has no .git, so the build script passes the number in as GWL_BUILD_NUMBER.
if(DEFINED GWL_BUILD_NUMBER)
    set(GWL_BUILD ${GWL_BUILD_NUMBER})
else()
    execute_process(COMMAND git rev-list --count HEAD WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
                    OUTPUT_VARIABLE GWL_BUILD OUTPUT_STRIP_TRAILING_WHITESPACE ERROR_QUIET RESULT_VARIABLE GWL_BUILD_RESULT)
    if(NOT GWL_BUILD_RESULT EQUAL 0)
        set(GWL_BUILD 0)
    endif()
endif()
target_compile_definitions(gwl_core PRIVATE GWL_BUILD=${GWL_BUILD})
target_include_directories(gwl_core PUBLIC include include/tray)

# Third-party code is kept in backend/third_party, each library with its own LICENSE.txt.
set(GWL_THIRD_PARTY "${CMAKE_CURRENT_SOURCE_DIR}/third_party")

target_include_directories(gwl_core PUBLIC "${GWL_THIRD_PARTY}/nlohmann/include")

if(WIN32)
    # WebView2.h is vendored in third_party/webview2, and WebView2Loader.dll is loaded at runtime.
    set(WEBVIEW2_INCLUDE_DIR "${GWL_THIRD_PARTY}/webview2/include" CACHE PATH "Folder holding WebView2.h")
    # Windows is built with MinGW only.
    if(NOT CMAKE_CXX_COMPILER_ID STREQUAL "GNU")
        message(FATAL_ERROR "The native Windows build uses MinGW. Configure it with the x86_64-w64-mingw32 toolchain.")
    endif()
    target_sources(gwl_core PRIVATE platform/windows/WebViewWindow.cpp platform/windows/Desktop.cpp platform/windows/Launch.cpp platform/windows/Network.cpp platform/windows/Crypto.cpp platform/windows/PasswordCache.cpp platform/windows/LauncherUpdate.cpp platform/windows/Wof.cpp platform/windows/Game.cpp platform/windows/Cli.cpp platform/windows/LaunchCli.cpp platform/windows/SystemInfo.cpp platform/windows/Process.cpp platform/windows/Shell.cpp platform/windows/Instance.cpp platform/windows/Tray.cpp platform/windows/WebView2.cpp)
    target_include_directories(gwl_core PRIVATE ${WEBVIEW2_INCLUDE_DIR} platform/windows/compat)
    target_link_libraries(gwl_core PUBLIC user32 ole32 shell32 shlwapi uuid comdlg32 ws2_32 bcrypt advapi32 crypt32)
elseif(UNIX AND NOT APPLE)
    # Carry account encryption in the executable rather than requiring the host's libcrypto.
    set(OPENSSL_USE_STATIC_LIBS TRUE)
    find_package(OpenSSL 3.0 MODULE REQUIRED COMPONENTS Crypto)
    if(NOT OPENSSL_CRYPTO_LIBRARY MATCHES "\\.a$")
        message(FATAL_ERROR "The Linux launcher requires static libcrypto. Install its development archive and clear cached OpenSSL paths.")
    endif()
    # The headers only: the libraries themselves are opened at runtime, so they're not linked.
    # GTK, GLib, GIO and WebKitGTK are declared by the stub headers in third_party/gtk, so the build needs none of their development
    # packages, and no pkg-config. The libraries are loaded at runtime, as the windows are.
    target_sources(gwl_core PRIVATE platform/linux/WebViewWindow.cpp platform/linux/Desktop.cpp platform/linux/Launch.cpp platform/linux/Network.cpp platform/linux/Crypto.cpp platform/linux/PasswordCache.cpp platform/linux/LauncherUpdate.cpp platform/linux/Wof.cpp platform/linux/SystemInfo.cpp platform/linux/Process.cpp platform/linux/Shell.cpp platform/linux/Instance.cpp platform/linux/Tray.cpp platform/linux/TrayPosition.cpp)
    target_include_directories(gwl_core PRIVATE "${GWL_THIRD_PARTY}/gtk/include")
    target_link_libraries(gwl_core PUBLIC OpenSSL::Crypto ${CMAKE_DL_LIBS})
else()
    message(FATAL_ERROR "The native layer supports Windows and Linux only.")
endif()

if(WIN32 AND GWL_CLI_ONLY)
    add_executable(gwlauncher_cli src/CliMain.cpp)
    set_target_properties(gwlauncher_cli PROPERTIES OUTPUT_NAME GW_Launcher.cli)
    target_link_libraries(gwlauncher_cli PRIVATE gwl_core)
    target_link_options(gwlauncher_cli PRIVATE -static)
endif()

if(NOT GWL_CLI_ONLY)
# The launcher. Its page and icons are compiled in from the frontend/ and assets/ folders, so the executable needs no files beside it.
set(GWL_ASSETS "${CMAKE_CURRENT_SOURCE_DIR}/../assets")
set(GWL_FRONTEND "${CMAKE_CURRENT_SOURCE_DIR}/../frontend")
set(GWL_ASSET_SOURCES "")
function(gwl_embed input function)
    set(output "${CMAKE_CURRENT_BINARY_DIR}/generated/${function}.cpp")
    # Written now as well as at build time, so the generated sources exist for the IDE's indexer before the first build.
    execute_process(COMMAND ${CMAKE_COMMAND} -DINPUT=${input} -DOUTPUT=${output} -DFUNCTION=${function}
                    -P ${CMAKE_CURRENT_SOURCE_DIR}/cmake/EmbedFile.cmake)
    add_custom_command(OUTPUT "${output}"
        COMMAND ${CMAKE_COMMAND} -DINPUT=${input} -DOUTPUT=${output} -DFUNCTION=${function}
                -P ${CMAKE_CURRENT_SOURCE_DIR}/cmake/EmbedFile.cmake
        DEPENDS "${input}" "${CMAKE_CURRENT_SOURCE_DIR}/cmake/EmbedFile.cmake"
        COMMENT "Embedding ${function}")
    set(GWL_ASSET_SOURCES ${GWL_ASSET_SOURCES} "${output}" PARENT_SCOPE)
endfunction()

if(EXISTS "${GWL_FRONTEND}/dist/index.html")
    # The page is stored gzip-compressed, and the launcher decompresses it, so the executable carries about a third of its size.
    set(GWL_PAGE_GZ "${CMAKE_CURRENT_BINARY_DIR}/generated/index.html.gz")
    file(MAKE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/generated")
    execute_process(COMMAND ${CMAKE_COMMAND} -DINPUT=${GWL_FRONTEND}/dist/index.html -DOUTPUT=${GWL_PAGE_GZ}
                    -P ${CMAKE_CURRENT_SOURCE_DIR}/cmake/GzipFile.cmake RESULT_VARIABLE gzip_result)
    if(NOT gzip_result EQUAL 0)
        message(FATAL_ERROR "The page couldn't be compressed.")
    endif()
    add_custom_command(OUTPUT "${GWL_PAGE_GZ}"
        COMMAND ${CMAKE_COMMAND} -DINPUT=${GWL_FRONTEND}/dist/index.html -DOUTPUT=${GWL_PAGE_GZ}
                -P ${CMAKE_CURRENT_SOURCE_DIR}/cmake/GzipFile.cmake
        DEPENDS "${GWL_FRONTEND}/dist/index.html" "${CMAKE_CURRENT_SOURCE_DIR}/cmake/GzipFile.cmake"
        COMMENT "Compressing the page")
    gwl_embed("${GWL_PAGE_GZ}" embeddedPageGzip)
    gwl_embed("${GWL_ASSETS}/gwlauncher-helmet-128.png" embeddedHelmetPng)
    gwl_embed("${GWL_ASSETS}/stylesheet-override-template.txt" embeddedOverrideTemplate)
    # The executable's icon: resource 1 is the helmet, the same icon the Rust launcher's build gives its executable. The resource
    # script must be part of the executable's own sources, so it's generated before the executable is declared.
    set(GWL_RC_SOURCES "")
    if(WIN32)
        enable_language(RC)
        set(GWL_ICON "${GWL_ASSETS}/gwlauncher-helmet-128.ico")
        # Resource 2 is the WebView2 loader, which the launcher writes beside itself at start and loads from there.
        file(WRITE "${CMAKE_CURRENT_BINARY_DIR}/launcher.rc"
             "1 ICON \"${GWL_ICON}\"\n2 RCDATA \"${GWL_WEBVIEW2_LOADER}\"\n")
        set_source_files_properties("${CMAKE_CURRENT_BINARY_DIR}/launcher.rc" PROPERTIES OBJECT_DEPENDS "${GWL_WEBVIEW2_LOADER}")
        set(GWL_RC_SOURCES "${CMAKE_CURRENT_BINARY_DIR}/launcher.rc")
    endif()
    add_executable(gwlauncher src/main.cpp src/AppState.cpp src/Launching.cpp src/tray/TrayMode.cpp ${GWL_RC_SOURCES}
                   src/windows/AccountListWindow.cpp src/windows/AccountEditWindow.cpp src/windows/ContextMenuWindow.cpp
                   src/windows/PromptWindow.cpp src/Progress.cpp src/AppUpdate.cpp src/windows/MessageWindow.cpp src/windows/IssuesWindow.cpp src/windows/SettingsWindow.cpp src/windows/SetupWindow.cpp src/windows/ProgressWindow.cpp src/Setup.cpp src/QuickLaunch.cpp
                   ${GWL_ASSET_SOURCES})
    target_include_directories(gwlauncher PRIVATE include/windows)
    target_link_libraries(gwlauncher PRIVATE gwl_core)
    # The release names the launcher GW_Launcher, which older launchers look for in the release.
    set_target_properties(gwlauncher PROPERTIES OUTPUT_NAME GW_Launcher)

    if(WIN32)
        target_link_options(gwlauncher PRIVATE -mwindows -static)
    else()
        set(GWL_CLI_EXE "${CMAKE_CURRENT_BINARY_DIR}/windows-cli/GW_Launcher.cli.exe")
        set_source_files_properties(src/EmbeddedCli.cpp PROPERTIES
            COMPILE_DEFINITIONS "GWL_CLI_PATH=\"${GWL_CLI_EXE}\""
            OBJECT_DEPENDS "${GWL_CLI_EXE}")
        target_sources(gwlauncher PRIVATE src/EmbeddedCli.cpp)
        # The Linux launcher's command-line launcher is built with MinGW, by a nested Windows build with the windows-release flags.
        find_program(GWL_MINGW_CXX x86_64-w64-mingw32-g++)
        if(NOT GWL_MINGW_CXX)
            message(FATAL_ERROR "The Linux launcher carries the Windows command-line launcher, so it's built with MinGW: install x86_64-w64-mingw32-g++.")
        endif()
        # The helper stays inside the Linux build folder so Windows packages need only the main executable.
        file(GLOB_RECURSE GWL_CLI_SOURCES CONFIGURE_DEPENDS
             "${CMAKE_CURRENT_SOURCE_DIR}/src/*" "${CMAKE_CURRENT_SOURCE_DIR}/include/*"
             "${CMAKE_CURRENT_SOURCE_DIR}/platform/windows/*" "${CMAKE_CURRENT_SOURCE_DIR}/third_party/*")
        list(APPEND GWL_CLI_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/CMakeLists.txt")
        set(GWL_CLI_CXX_FLAGS "-O2 -DNDEBUG -ffunction-sections -fdata-sections -flto=auto")
        set(GWL_CLI_LINK_FLAGS "-Wl,--gc-sections -flto=auto -O2 -s")
        add_custom_command(OUTPUT "${GWL_CLI_EXE}"
            COMMAND ${CMAKE_COMMAND} -G "${CMAKE_GENERATOR}" -S "${CMAKE_CURRENT_SOURCE_DIR}" -B "${CMAKE_CURRENT_BINARY_DIR}/windows-cli"
                    -DGWL_CLI_ONLY=ON -DGWL_BUILD_NUMBER=${GWL_BUILD} -DCMAKE_TOOLCHAIN_FILE=${CMAKE_CURRENT_SOURCE_DIR}/cmake/toolchains/mingw-w64.cmake
                    -DCMAKE_BUILD_TYPE=Release "-DCMAKE_CXX_FLAGS_RELEASE=${GWL_CLI_CXX_FLAGS}" "-DCMAKE_EXE_LINKER_FLAGS=${GWL_CLI_LINK_FLAGS}"
            COMMAND ${CMAKE_COMMAND} --build "${CMAKE_CURRENT_BINARY_DIR}/windows-cli" --target gwlauncher_cli
            DEPENDS ${GWL_CLI_SOURCES}
            COMMENT "Building GW_Launcher.cli.exe for the Linux launcher"
            VERBATIM)
        add_custom_target(gwlauncher_cli_windows DEPENDS "${GWL_CLI_EXE}")
        add_dependencies(gwlauncher gwlauncher_cli_windows)
    endif()

    # A Linux executable can't carry an icon, so a desktop entry beside it gives the file manager one. The window's class matches
    # the entry's StartupWMClass, so the taskbar groups the window with it.
    if(UNIX AND NOT APPLE)
        get_filename_component(GWL_BIN_ABS "${GWL_BIN_DIR}" ABSOLUTE)
        file(COPY "${GWL_ASSETS}/gwlauncher-helmet-128.png" DESTINATION "${GWL_BIN_ABS}")
        file(WRITE "${GWL_BIN_ABS}/GW_Launcher.desktop" "[Desktop Entry]\nType=Application\nName=GW Launcher\nComment=Launches Guild Wars accounts\nExec=${GWL_BIN_ABS}/GW_Launcher\nIcon=${GWL_BIN_ABS}/gwlauncher-helmet-128.png\nStartupWMClass=gwlauncher\nTerminal=false\nCategories=Game;\n")
    endif()
else()
    message(FATAL_ERROR "The page isn't built: run build.sh, or 'cd frontend && npm ci && npm run build' first.")
endif()
endif()
