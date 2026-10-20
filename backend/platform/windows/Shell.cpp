// Windows: the clipboard, the shell's open and select, the desktop folder, and .lnk shortcuts through the shell link object.

#include "Shell.h"
#include "utils/Files.h"

#include <windows.h>
#include <knownfolders.h>
#include <objbase.h>
#include <shlobj.h>
#include <shobjidl.h>

#include <cstring>
#include <iterator>
#include <stdexcept>
#include <string>

namespace gwl {

namespace {

namespace fs = std::filesystem;

std::wstring wide(const std::string& text) {
    const int length = MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, nullptr, 0);
    std::wstring result(length > 0 ? length - 1 : 0, L'\0');
    if (length > 0) {
        MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, result.data(), length);
    }
    return result;
}

}  // namespace

void copyText(const std::string& text) {
    const std::wstring wideText = wide(text);
    if (!OpenClipboard(nullptr)) {
        return;
    }
    EmptyClipboard();
    const SIZE_T bytes = (wideText.size() + 1) * sizeof(wchar_t);
    if (HGLOBAL memory = GlobalAlloc(GMEM_MOVEABLE, bytes)) {
        if (void* target = GlobalLock(memory)) {
            std::memcpy(target, wideText.c_str(), bytes);
            GlobalUnlock(memory);
            SetClipboardData(CF_UNICODETEXT, memory);
        } else {
            GlobalFree(memory);
        }
    }
    CloseClipboard();
}

void openUrl(const std::string& url) {
    ShellExecuteW(nullptr, L"open", wide(url).c_str(), nullptr, nullptr, SW_SHOWNORMAL);
}

void revealInFileExplorer(const fs::path& path) {
    const std::wstring arguments = L"/select,\"" + path.wstring() + L"\"";
    ShellExecuteW(nullptr, L"open", L"explorer.exe", arguments.c_str(), nullptr, SW_SHOWNORMAL);
}

std::optional<fs::path> desktopFolder() {
    PWSTR folder = nullptr;
    if (FAILED(SHGetKnownFolderPath(FOLDERID_Desktop, 0, nullptr, &folder))) {
        return std::nullopt;
    }
    fs::path result(folder);
    CoTaskMemFree(folder);
    return result;
}

fs::path executablePath() {
    wchar_t buffer[MAX_PATH * 4];
    const DWORD length = GetModuleFileNameW(nullptr, buffer, static_cast<DWORD>(std::size(buffer)));
    return fs::path(std::wstring(buffer, length));
}

void createShortcut(const fs::path& file, const fs::path& target, const std::string& arguments, const std::string& description,
                    const std::string&) {
    // COM is initialised on this thread for the call, and released after it when this call started it.
    const HRESULT initialized = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    IShellLinkW* link = nullptr;
    HRESULT result = CoCreateInstance(CLSID_ShellLink, nullptr, CLSCTX_INPROC_SERVER, IID_IShellLinkW, reinterpret_cast<void**>(&link));
    if (SUCCEEDED(result)) {
        link->SetPath(target.wstring().c_str());
        link->SetArguments(wide(arguments).c_str());
        link->SetWorkingDirectory(target.parent_path().wstring().c_str());
        link->SetDescription(wide(description).c_str());
        IPersistFile* persist = nullptr;
        result = link->QueryInterface(IID_IPersistFile, reinterpret_cast<void**>(&persist));
        if (SUCCEEDED(result)) {
            result = persist->Save(file.wstring().c_str(), TRUE);
            persist->Release();
        }
        link->Release();
    }
    if (SUCCEEDED(initialized)) {
        CoUninitialize();
    }
    if (FAILED(result)) {
        throw std::runtime_error("Could not write the shortcut: " + pathToUtf8(file));
    }
}

}  // namespace gwl
