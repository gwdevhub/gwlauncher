#include <malloc.h>
#include <Windows.h>
#include <stdio.h>
#include <sddl.h>
#include <winsafer.h>

#define GWML_NO_DATFIX				1
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
            IN HANDLE               ProcessHandle,
            IN PROCESS_INFORMATION_CLASS ProcessInformationClass,
            OUT PVOID               ProcessInformation,
            IN ULONG                ProcessInformationLength,
            OUT PULONG              ReturnLength);

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

    if (!ReadProcessMemory(hProcess, (void*)g_moduleBase, g_gwdata, 0x48D000, NULL)) {
        return FALSE;
    }

    for (DWORD i = 0; i < 0x48D000; ++i) {
        if (!memcmp(g_gwdata + i, sig_patch, sizeof(sig_patch))) {
            mcpatch = (BYTE*)(g_moduleBase + i - 0x1A);
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

const unsigned char payload[] = {
    0x51,                                   // | PUSH ECX  
    0x52,                                   // | PUSH EDX                                       
    0x50,                                   // | PUSH EAX                                       
    0x3E, 0x8B, 0x4C, 0x24, 0x10,                         // | MOV ECX, DWORD PTR DS:[ESP + 10]               
    0x33, 0xD2,                                 // | XOR EDX, EDX                                   
    0x33, 0xC0,                                 // | XOR EAX, EAX                                   
    0x3E, 0x66, 0x8B, 0x04, 0x11,                          // | MOV AX, WORD PTR DS:[ECX + EDX]                
    0x66, 0x83, 0xF8, 0x00,                           // | CMP AX, 0                                      
    0x74, 0x05,                                // | JE <gwml.getbackslash>                         
    0x83, 0xC2, 0x02,                              // | ADD EDX, 2                                     
    0xEB, 0xF0,                                // | JMP <gwml.getlen>                              
    0x83, 0xEA, 0x02,                              // | SUB EDX, 2                                     
    0x3E, 0x66, 0x8B, 0x04, 0x11,                          // | MOV AX, WORD PTR DS:[ECX + EDX]                
    0x66, 0x83, 0xF8, 0x5C,                           // | CMP AX, 5C                                     
    0x74, 0x02,                                // | JE <gwml.checkifdat>                           
    0xEB, 0xF0,                                // | JMP <gwml.getbackslash>                        
    0x83, 0xC2, 0x02,                              // | ADD EDX, 2                                     
    0x3E, 0x66, 0x8B, 0x04, 0x11,                          // | MOV AX, WORD PTR DS:[ECX + EDX]                
    0x66, 0x83, 0xF8, 0x47,                           // | CMP AX, 47                                     
    0x75, 0x66,                                // | JNE <gwml.exitwithoutset>                      
    0x83, 0xC2, 0x02,                              // | ADD EDX, 2                                     
    0x3E, 0x66, 0x8B, 0x04, 0x11,                          // | MOV AX, WORD PTR DS:[ECX + EDX]                
    0x66, 0x83, 0xF8, 0x77,                           // | CMP AX, 77                                     
    0x75, 0x58,                                // | JNE <gwml.exitwithoutset>                      
    0x83, 0xC2, 0x02,                              // | ADD EDX, 2                                     
    0x3E, 0x66, 0x8B, 0x04, 0x11,                          // | MOV AX, WORD PTR DS:[ECX + EDX]                
    0x66, 0x83, 0xF8, 0x2E,                           // | CMP AX, 2E                                     
    0x75, 0x4A,                                // | JNE <gwml.exitwithoutset>                      
    0x83, 0xC2, 0x02,                              // | ADD EDX, 2                                     
    0x3E, 0x66, 0x8B, 0x04, 0x11,                          // | MOV AX, WORD PTR DS:[ECX + EDX]                
    0x66, 0x83, 0xF8, 0x64,                           // | CMP AX, 64                                     
    0x75, 0x3C,                                // | JNE <gwml.exitwithoutset>                      
    0x83, 0xC2, 0x02,                              // | ADD EDX, 2                                     
    0x3E, 0x66, 0x8B, 0x04, 0x11,                          // | MOV AX, WORD PTR DS:[ECX + EDX]                
    0x66, 0x83, 0xF8, 0x61,                           // | CMP AX, 61                                     
    0x75, 0x2E,                                // | JNE <gwml.exitwithoutset>                      
    0x83, 0xC2, 0x02,                              // | ADD EDX, 2                                     
    0x3E, 0x66, 0x8B, 0x04, 0x11,                          // | MOV AX, WORD PTR DS:[ECX + EDX]                
    0x66, 0x83, 0xF8, 0x74,                           // | CMP AX, 74                                     
    0x75, 0x20,                                // | JNE <gwml.exitwithoutset>                      
    0x58,                                   // | POP EAX                                        
    0x5A,                                   // | POP EDX                                        
    0x59,                                   // | POP ECX                                        
    0x36, 0xC7, 0x44, 0x24, 0x08, 0x00, 0x00, 0x00, 0x80,                // | MOV DWORD PTR SS:[ESP + 8], 80000000           
    0x36, 0xC7, 0x44, 0x24, 0x0C, 0x03, 0x00, 0x00, 0x00,                // | MOV DWORD PTR SS:[ESP + C], 3                  
    0x36, 0xC7, 0x44, 0x24, 0x18, 0x01, 0x00, 0x00, 0x00,                // | MOV DWORD PTR SS:[ESP + 18], 1                 
    0xEB, 0x03,                                // | JMP <gwml.end>
    0x58,                                   // | POP EAX                                        
    0x5A,                                   // | POP EDX                                        
    0x59,       // | POP ECX
    0x55,
    0x8B, 0xEC,
    0x81, 0xEC, 0x14, 0x01, 0x00, 0x00,
    0xE9
};

#define ENCODE_REL(from,to) (uintptr_t)((uintptr_t)(to) - (uintptr_t)(from) - 5)

__declspec(dllexport) BOOL DATFix(HANDLE hProcess)
{
    const BYTE sig_datfix[] = { 0x8B, 0x4D, 0x18, 0x8B, 0x55, 0x1C, 0x8B };

    BYTE* datfix = NULL;

    for (DWORD i = 0; i < 0x48D000; ++i) {
        if (!memcmp(g_gwdata + i, sig_datfix, sizeof(sig_datfix))) {
            datfix = (BYTE*)(g_moduleBase + i - 0x1A);
            break;
        }
    }

    void* asmbuffer = VirtualAllocEx(hProcess, NULL,
        sizeof(payload) + 0x20,
        MEM_COMMIT | MEM_RESERVE,
        PAGE_EXECUTE_READWRITE);

    // dump detour into internal mem
    ASSERT(WriteProcessMemory(hProcess, asmbuffer, (void*)payload, sizeof(payload), NULL));

    // write return to ofunc
    void* asmend = (char*)asmbuffer + sizeof(payload);



    DWORD rva_payload = ENCODE_REL((char*)asmbuffer + sizeof(payload) - 1, (uintptr_t)datfix + 9);
    ASSERT(WriteProcessMemory(hProcess, (void*)asmend, &rva_payload, sizeof(rva_payload), NULL));

    // write jmp to detour
    BYTE jmpencoding[5] = { 0xE9, 0, 0, 0, 0 };
    *(DWORD*)(jmpencoding + 1) = ENCODE_REL(datfix, asmbuffer);
    ASSERT(WriteProcessMemory(hProcess, datfix, jmpencoding, sizeof(jmpencoding), NULL));

    return TRUE;
}

__declspec(dllexport) DWORD LaunchClient(LPCWSTR path, LPCWSTR args, DWORD flags, DWORD* out_hThread)
{
#if 0
    AllocConsole();
    freopen("CONOUT$", "w", stdout);
#endif

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

    if ((flags & GWML_NO_DATFIX) == 0) {
        if (!DATFix(procinfo.hProcess)) {
            ResumeThread(procinfo.hThread);
            CloseHandle(procinfo.hThread);
            CloseHandle(procinfo.hProcess);
            MCERROR("DATFix");
        }
    }

    if (out_hThread != NULL)
        *out_hThread = (DWORD)procinfo.hThread;

    CloseHandle(procinfo.hProcess);
    return procinfo.dwProcessId;
}