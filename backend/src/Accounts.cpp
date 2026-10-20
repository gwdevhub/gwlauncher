#include "Accounts.h"

#include "utils/Files.h"
#include "utils/Strings.h"

#include <cctype>
#include <cstddef>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <sstream>
#include <stdexcept>
#include <utility>
#include <variant>

namespace gwl {

namespace {

// The keys of one account, in the C# order. Account fields that aren't required default when missing.
const char* const kRequired[] = {"character", "email", "gwpath", "password"};

std::string requiredString(const nlohmann::ordered_json& entry, const char* key) {
    if (!entry.contains(key) || !entry[key].is_string()) {
        throw std::runtime_error(std::string("Accounts.json is invalid: an account has no ") + key + ".");
    }
    return entry[key].get<std::string>();
}

std::string optionalString(const nlohmann::ordered_json& entry, const char* key) {
    return entry.contains(key) && entry[key].is_string() ? entry[key].get<std::string>() : std::string();
}

bool optionalBool(const nlohmann::ordered_json& entry, const char* key, bool fallback) {
    return entry.contains(key) && entry[key].is_boolean() ? entry[key].get<bool>() : fallback;
}

// The editor's text fields: the text without the spaces around it.
std::string stringField(const Json& json, const char* key) {
    return json.contains(key) && json[key].is_string() ? json[key].get<std::string>() : std::string();
}

bool boolField(const Json& json, const char* key, bool fallback) {
    return json.contains(key) && json[key].is_boolean() ? json[key].get<bool>() : fallback;
}

}  // namespace

std::string Account::name() const {
    for (const std::string* value : {&title, &character, &email}) {
        if (!value->empty()) {
            return *value;
        }
    }
    return character;
}

nlohmann::ordered_json Mod::fileJson() const {
    return {
        {"active", active},
        {"fileName", fileName},
        {"type", static_cast<int>(kind)},
    };
}

Mod Mod::fromFileJson(const nlohmann::ordered_json& item) {
    Mod mod;
    mod.active = optionalBool(item, "active", true);
    mod.fileName = optionalString(item, "fileName");
    const int type = item.contains("type") && item["type"].is_number_integer() ? item["type"].get<int>() : -1;
    if (type != 0 && type != 1) {
        throw std::runtime_error("Accounts.json is invalid: a mod has an unknown type.");
    }
    mod.kind = static_cast<ModType>(type);
    return mod;
}

Json Mod::draftJson() const {
    return {
        {"fileName", fileName},
        {"type", static_cast<int>(kind)},
        {"active", active},
        {"linuxPath", nullptr},
        {"target", nullptr},
    };
}

Mod Mod::fromDraftJson(const Json& item) {
    Mod mod;
    mod.fileName = stringField(item, "fileName");
    mod.active = boolField(item, "active", true);
    const int type = item.contains("type") && item["type"].is_number_integer() ? item["type"].get<int>() : -1;
    if (type != 0 && type != 1) {
        throw std::runtime_error("The draft has a mod of an unknown type.");
    }
    mod.kind = static_cast<ModType>(type);
    return mod;
}

nlohmann::ordered_json Account::fileJson() const {
    nlohmann::ordered_json list = nlohmann::ordered_json::array();
    for (const Mod& mod : mods) {
        list.push_back(mod.fileJson());
    }
    return {
        {"character", character},
        {"email", email},
        {"gwpath", gwpath},
        {"password", password},
        {"extraargs", extraargs},
        {"elevated", elevated},
        {"is_steam_account", isSteamAccount},
        {"title", title},
        {"usePluginFolderMods", usePluginFolderMods},
        {"mods", list},
    };
}

Account Account::fromFileJson(const nlohmann::ordered_json& entry) {
    if (!entry.is_object()) {
        throw std::runtime_error("Accounts.json is invalid: an account isn't an object.");
    }
    for (const char* key : kRequired) {
        if (!entry.contains(key)) {
            throw std::runtime_error(std::string("Accounts.json is invalid: an account has no ") + key + ".");
        }
    }
    Account account;
    account.character = requiredString(entry, "character");
    account.email = requiredString(entry, "email");
    account.gwpath = requiredString(entry, "gwpath");
    account.password = requiredString(entry, "password");
    account.extraargs = optionalString(entry, "extraargs");
    account.elevated = optionalBool(entry, "elevated", false);
    account.isSteamAccount = optionalBool(entry, "is_steam_account", false);
    account.title = optionalString(entry, "title");
    account.usePluginFolderMods = optionalBool(entry, "usePluginFolderMods", true);
    if (entry.contains("mods") && entry["mods"].is_array()) {
        for (const auto& item : entry["mods"]) {
            account.mods.push_back(Mod::fromFileJson(item));
        }
    }
    return account;
}

Json Account::listRow(const std::string& icon) const {
    return {
        {"name", name()},
        {"icon", icon},
        {"issues", Json::array()},
        {"blocked", false},
        {"state", "Inactive"},
    };
}

Json Account::draft() const {
    Json list = Json::array();
    for (const Mod& mod : mods) {
        list.push_back(mod.draftJson());
    }
    return {
        {"title", title},
        {"email", email},
        {"password", password},
        {"character", character},
        {"gwPath", gwpath},
        {"linuxGwPath", ""},
        {"extraArgs", extraargs},
        {"usePluginFolderMods", usePluginFolderMods},
        {"steamAccount", isSteamAccount},
        {"elevated", elevated},
        {"mods", list},
    };
}

Json Account::newDraft(const std::string& defaultPath) {
    return {
        {"title", ""},
        {"email", ""},
        {"password", ""},
        {"character", ""},
        {"gwPath", defaultPath},
        {"linuxGwPath", ""},
        {"extraArgs", ""},
        {"usePluginFolderMods", true},
        {"steamAccount", false},
        {"elevated", false},
        {"mods", Json::array()},
    };
}

std::variant<Account, std::vector<FieldError>> Account::fromDraft(const Json& draft) {
    std::vector<FieldError> errors;
    if (trimmed(stringField(draft, "title")).empty()) {
        errors.push_back({"title", "Enter a title for the account."});
    }

    const std::string path = trimmed(stringField(draft, "gwPath"));
    if (path.empty()) {
        errors.push_back({"gwPath", "Enter a path to Gw.exe."});
    } else if (!std::filesystem::is_regular_file(pathFromUtf8(path))) {
        errors.push_back({"gwPath", "Gw.exe was not found at that path."});
    }
    if (!errors.empty()) {
        return errors;
    }

    Account account;
    account.title = trimmed(stringField(draft, "title"));
    account.email = trimmed(stringField(draft, "email"));
    account.password = stringField(draft, "password");
    account.character = trimmed(stringField(draft, "character"));
    account.gwpath = path;
    account.extraargs = trimmed(stringField(draft, "extraArgs"));
    account.elevated = boolField(draft, "elevated", false);
    account.isSteamAccount = boolField(draft, "steamAccount", false);
    account.usePluginFolderMods = boolField(draft, "usePluginFolderMods", true);
    if (draft.contains("mods") && draft["mods"].is_array()) {
        for (const auto& item : draft["mods"]) {
            account.mods.push_back(Mod::fromDraftJson(item));
        }
    }
    return account;
}

namespace {

// The accounts in a JSON array, or nothing when the text isn't one.
std::optional<std::vector<Account>> accountsFromJson(const std::string& text) {
    nlohmann::ordered_json entries = nlohmann::ordered_json::parse(text, nullptr, false);
    if (entries.is_discarded() || !entries.is_array()) {
        return std::nullopt;
    }
    std::vector<Account> accounts;
    for (const auto& entry : entries) {
        accounts.push_back(Account::fromFileJson(entry));
    }
    return accounts;
}

}  // namespace

std::optional<OpenedAccounts> openAccounts(const std::string& path, const std::optional<PasswordHash>& password) {
    const auto read = readFileBytes(pathFromUtf8(path));
    if (!read) {
        saveAccounts({}, path, std::nullopt);
        return OpenedAccounts{};
    }
    const Bytes raw = *read;

    // Anything that parses as a list is plain text. Otherwise the file is encrypted, and needs the password.
    if (!hasEncryptionMarker(raw)) {
        const std::string text = withoutByteOrderMark(std::string(raw.begin(), raw.end()));
        if (auto accounts = accountsFromJson(text)) {
            return OpenedAccounts{std::move(*accounts), std::nullopt};
        }
    }
    if (!password) {
        return std::nullopt;
    }
    const auto decrypted = decryptAccountsFile(raw, *password);
    if (!decrypted) {
        return std::nullopt;
    }
    // The C# writer puts a byte-order mark at the start of the encrypted JSON. A list that doesn't read as accounts is treated as
    // the wrong password, as the Rust launcher does.
    std::optional<std::vector<Account>> accounts;
    try {
        accounts = accountsFromJson(withoutByteOrderMark(*decrypted));
    } catch (const std::runtime_error&) {
        return std::nullopt;
    }
    if (!accounts) {
        return std::nullopt;
    }
    // Files from before the marker are rewritten in the current format.
    if (!hasEncryptionMarker(raw)) {
        saveAccounts(*accounts, path, password);
    }
    return OpenedAccounts{std::move(*accounts), password};
}

void saveAccounts(const std::vector<Account>& accounts, const std::string& path, const std::optional<PasswordHash>& password) {
    nlohmann::ordered_json list = nlohmann::ordered_json::array();
    for (const Account& account : accounts) {
        list.push_back(account.fileJson());
    }
    std::string text = list.dump(2);
    std::string crlf;
    crlf.reserve(text.size() + text.size() / 16);
    for (char c : text) {
        if (c == '\n') {
            crlf += '\r';
        }
        crlf += c;
    }
    const Bytes bytes = password ? encryptAccountsFile(crlf, *password) : Bytes(crlf.begin(), crlf.end());
    if (!writeFileBytes(pathFromUtf8(path), bytes)) {
        throw std::runtime_error("Accounts.json couldn't be written.");
    }
}

Json failureJson(const std::vector<FieldError>& errors) {
    Json fields = Json::array();
    for (const FieldError& error : errors) {
        fields.push_back(error.field);
    }
    return {{"message", errors.front().message}, {"fields", fields}};
}

void putAccount(std::vector<Account>& accounts, std::optional<std::size_t> index, Account account) {
    if (!index) {
        accounts.push_back(std::move(account));
        return;
    }
    if (*index >= accounts.size()) {
        throw std::runtime_error("that account isn't in the list");
    }
    accounts[*index] = std::move(account);
}

void removeAccount(std::vector<Account>& accounts, std::size_t index) {
    if (index >= accounts.size()) {
        throw std::runtime_error("that account isn't in the list");
    }
    accounts.erase(accounts.begin() + static_cast<std::ptrdiff_t>(index));
}

void moveAccount(std::vector<Account>& accounts, std::size_t from, std::size_t to) {
    if (from >= accounts.size() || to >= accounts.size()) {
        throw std::runtime_error("that account isn't in the list");
    }
    Account account = std::move(accounts[from]);
    accounts.erase(accounts.begin() + static_cast<std::ptrdiff_t>(from));
    accounts.insert(accounts.begin() + static_cast<std::ptrdiff_t>(to), std::move(account));
}

}  // namespace gwl
