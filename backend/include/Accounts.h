#pragma once

#include <cstddef>
#include <optional>
#include <string>
#include <variant>
#include <vector>

#include "AccountFile.h"
#include "Messages.h"

namespace gwl {

// The mod types, numbered as the C# enum serialises them.
enum class ModType : int { Texmod = 0, Dll = 1 };

// A problem with one of the editor's fields, named by the page's field key.
struct FieldError {
    std::string field;
    std::string message;
};

struct Mod {
    bool active = true;
    std::u16string fileName;
    ModType kind = ModType::Dll;

    // The mod as Accounts.json holds it.
    nlohmann::ordered_json fileJson() const;
    // The mod read from Accounts.json. Throws when its type is unknown.
    static Mod fromFileJson(const nlohmann::ordered_json& item);

    // The mod as the account editor's draft holds it.
    Json draftJson() const;
    // The mod read from the editor's draft. Throws when its type is unknown.
    static Mod fromDraftJson(const Json& item);
};

// One account, with the fields and names of the C# Account class.
struct Account {
    std::u16string character;
    std::u16string email;
    std::u16string gwpath;
    std::u16string password;
    std::u16string extraargs;
    bool elevated = false;
    bool isSteamAccount = false;
    std::u16string title;
    bool usePluginFolderMods = true;
    std::vector<Mod> mods;

    // The name shown in the list: the title, then the character, then the email.
    std::u16string name() const;

    // The account as Accounts.json holds it, which is also the form the launcher's helper reads.
    nlohmann::ordered_json fileJson() const;
    // The account read from Accounts.json. Throws when the entry isn't an account, or lacks a required field.
    static Account fromFileJson(const nlohmann::ordered_json& entry);

    // The account as a list row: its name, its icon as a data URL, and its state.
    Json listRow(const std::string& icon) const;

    // The path a running game is listed under: the Linux path, in lower case. Missing when the path isn't on this computer.
    // Defined in src/Launching.cpp, where the platform's paths are used.
    std::optional<std::u16string> pathKey() const;

    // The editor's starting fields for this account, and for a new account at the default install path (empty when there's none).
    Json draft() const;
    static Json newDraft(const std::u16string& defaultPath);
    // The account the draft describes, or every problem with its fields. The title is required, and Gw.exe must exist.
    static std::variant<Account, std::vector<FieldError>> fromDraft(const Json& draft);
};

// An Accounts.json that opened: its accounts, and the password it's encrypted with, or nothing when it's plain text.
struct OpenedAccounts {
    std::vector<Account> accounts;
    std::optional<PasswordHash> password;
};

// Opens Accounts.json. A missing file is written as an empty list. A plain file opens without a password. An encrypted file opens
// only with its password, and nothing comes back when the password is missing or wrong. Throws when the file can't be read, or
// holds an account without its required fields.
std::optional<OpenedAccounts> openAccounts(const std::string& path, const std::optional<PasswordHash>& password);

// Writes the accounts as the C# Save does: indented, the same key order, CRLF line endings. With a password, the file is encrypted.
void saveAccounts(const std::vector<Account>& accounts, const std::string& path, const std::optional<PasswordHash>& password);

// The save failure the page shows: the first message, and each field that's wrong.
Json failureJson(const std::vector<FieldError>& errors);

// Replaces the account at index, or adds one at the end when there's no index. Throws when index isn't in the list.
void putAccount(std::vector<Account>& accounts, std::optional<std::size_t> index, Account account);

// Removes the account at index. Throws when index isn't in the list.
void removeAccount(std::vector<Account>& accounts, std::size_t index);

// Takes the account at from and puts it at to, as the C# AccountManager does. Throws when either isn't in the list.
void moveAccount(std::vector<Account>& accounts, std::size_t from, std::size_t to);

}  // namespace gwl
