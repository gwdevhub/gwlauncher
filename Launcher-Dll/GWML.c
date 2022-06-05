#include <malloc.h>
#include <Windows.h>
#include <stdio.h>
#include <sddl.h>
#include <winsafer.h>

#define GWML_KEEP_SUSPENDED 		2
#define GWML_ELEVATED               8

#define MCERROR(msg) do { MessageBoxA(0, "GWML - Assert Fail",msg,0); return FALSE; } while(0)
#define ASSERT(action) do { if(!( action )) MCERROR(#action); } while(0)


PBYTE g_moduleBase = NULL;
BYTE g_gwdata[0x48D000];


PBYTE GetProcessModuleBase(HANDLE process)
{

    typedef
        NTSTATUS
        NTAPI
        NtQueryInformationProcess_t(
            IN HANDLE                       ProcessHandle,
            IN PROCESS_INFORMATION_CLASS    ProcessInformationClass,
            OUT PVOID                       ProcessInformation,
            IN ULONG                        ProcessInformationLength,
            OUT PULONG                      ReturnLength);

    struct PEB
    {
        UCHAR InheritedAddressSpace;
        UCHAR ReadImageFileExecOptions;
        UCHAR BeingDebugged;
        UCHAR BitField;
        PVOID Mutant;
        PVOID ImageBaseAddress;
    };

    struct PROCESS_BASIC_INFORMATION {
        PVOID Reserved1;
        struct PEB* PebBaseAddress;
        PVOID Reserved2[2];
        ULONG_PTR UniqueProcessId;
        PVOID Reserved3;
    };

    NtQueryInformationProcess_t* NtQueryInformationProcess = (NtQueryInformationProcess_t*)GetProcAddress(GetModuleHandleA("ntdll.dll"), "NtQueryInformationProcess");
    struct PROCESS_BASIC_INFORMATION pbi;
    struct PEB peb;
    ULONG retLen;
    if (NtQueryInformationProcess(process, 0, &pbi, sizeof(pbi), &retLen))
    {
        return NULL;
    }

    if (!ReadProcessMemory(process, pbi.PebBaseAddress, &peb, sizeof(peb), &retLen))
    {
        return NULL;
    }

    return (PBYTE)peb.ImageBaseAddress + 0x1000;
}


__declspec(dllexport) BOOL MCPatch(HANDLE hProcess) {
    const BYTE sig_patch[] = { 0x56, 0x57, 0x68, 0x00, 0x01, 0x00, 0x00, 0x89, 0x85, 0xF4, 0xFE, 0xFF, 0xFF, 0xC7, 0x00, 0x00, 0x00, 0x00, 0x00 };

    BYTE* mcpatch = NULL;

    if (!ReadProcessMemory(hProcess, g_moduleBase, g_gwdata, 0x48D000, NULL)) {
        return FALSE;
    }

    for (DWORD i = 0; i < 0x48D000; ++i) {
        if (!memcmp(g_gwdata + i, sig_patch, sizeof sig_patch)) {
            mcpatch = g_moduleBase + i - 0x1A;
            break;
        }
    }
    if (!mcpatch)
        return FALSE;

    printf("mcpatch = %p\n", mcpatch);

    const BYTE payload[] = { 0x31, 0xC0, 0x90, 0xC3 };

    if (!WriteProcessMemory(hProcess, mcpatch, payload, sizeof(payload), NULL))
        MCERROR("WriteProcessMemory mcpatch");

    return TRUE;
}

__declspec(dllexport) DWORD LaunchClient(LPCWSTR path, LPCWSTR args, DWORD flags, DWORD* out_hThread)
{
    WCHAR commandLine[0x100];
    swprintf(commandLine, 0x100, L"\"%s\" %s", path, args);


    STARTUPINFOW startinfo = { 0 };
    PROCESS_INFORMATION procinfo = { 0 };

    WCHAR last_directory[MAX_PATH];
    GetCurrentDirectoryW(MAX_PATH, last_directory);
    WCHAR* trial = wcsstr(path, L"Gw.exe");
    trial[0] = L'\0';
    SetCurrentDirectoryW(path);

    if (!(flags & GWML_ELEVATED)) {
        SAFER_LEVEL_HANDLE hLevel = NULL;
        if (!SaferCreateLevel(SAFER_SCOPEID_USER, SAFER_LEVELID_NORMALUSER, SAFER_LEVEL_OPEN, &hLevel, NULL))
        {
            MCERROR("SaferCreateLevel");
        }

        HANDLE hRestrictedToken = NULL;
        if (!SaferComputeTokenFromLevel(hLevel, NULL, &hRestrictedToken, 0, NULL))
        {
            SaferCloseLevel(hLevel);
            MCERROR("SaferComputeTokenFromLevel");
        }

        SaferCloseLevel(hLevel);

        // Set the token to medium integrity.

        TOKEN_MANDATORY_LABEL tml = { 0 };
        tml.Label.Attributes = SE_GROUP_INTEGRITY;
        if (!ConvertStringSidToSid(TEXT("S-1-16-8192"), &(tml.Label.Sid)))
        {
            CloseHandle(hRestrictedToken);
            MCERROR("ConvertStringSidToSid");
        }

        if (!SetTokenInformation(hRestrictedToken, TokenIntegrityLevel, &tml, sizeof(tml) + GetLengthSid(tml.Label.Sid)))
        {
            LocalFree(tml.Label.Sid);
            CloseHandle(hRestrictedToken);
            return FALSE;
        }

        if (!CreateProcessAsUserW(hRestrictedToken, NULL, commandLine, NULL, NULL, FALSE, CREATE_SUSPENDED , NULL, NULL, &startinfo, &procinfo)) {
            MCERROR("CreateProcessAsUserW");
        }

        CloseHandle(hRestrictedToken);
    }
    else {
        if (!CreateProcessW(NULL, commandLine, NULL, NULL, FALSE, CREATE_SUSPENDED, NULL, NULL, &startinfo, &procinfo))
            MCERROR("CreateProcessW");
    }

    SetCurrentDirectoryW(last_directory);
    trial[0] = L'G';

    g_moduleBase = GetProcessModuleBase(procinfo.hProcess);

    if (!MCPatch(procinfo.hProcess)) {
        ResumeThread(procinfo.hThread);
        CloseHandle(procinfo.hThread);
        CloseHandle(procinfo.hProcess);
        MCERROR("MCPatch");
    }
    
    if (out_hThread != NULL)
        *out_hThread = (DWORD)procinfo.hThread;

    CloseHandle(procinfo.hProcess);
    return procinfo.dwProcessId;
}