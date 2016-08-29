#include "GWML.h"
#include <TlHelp32.h>

typedef struct _LSA_UNICODE_STRING {
	USHORT Length;
	USHORT MaximumLength;
	PWSTR  Buffer;
} LSA_UNICODE_STRING, *PLSA_UNICODE_STRING, UNICODE_STRING, *PUNICODE_STRING;

typedef struct _SYSTEM_HANDLE
{
	DWORD    dwProcessId;
	BYTE     bObjectType;
	BYTE     bFlags;
	WORD     wValue;
	HANDLE    pHandle;
	DWORD    GrantedAccess;
} SYSTEM_HANDLE, *PSYSTEM_HANDLE;

typedef struct _SYSTEM_HANDLE_INFORMATION
{
	ULONG HandleCount;
	SYSTEM_HANDLE Handles[1];
} SYSTEM_HANDLE_INFORMATION, *PSYSTEM_HANDLE_INFORMATION;

typedef struct _OBJECT_BASIC_INFORMATION
{
	UINT Attributes;
	UINT GrantedAccess;
	UINT HandleCount;
	UINT PointerCount;
	UINT PagedPoolUsage;
	UINT NonPagedPoolUsage;
	UINT Reserved1;
	UINT Reserved2;
	UINT Reserved3;
	UINT NameInformationLength;
	UINT TypeInformationLength;
	UINT SecurityDescriptorLength;
	struct {
		DWORD dwLowDateTime;
		DWORD dwHighDateTime;
	};
} OBJECT_BASIC_INFORMATION, *POBJECT_BASIC_INFORMATION;

typedef NTSTATUS(NTAPI *_NtQuerySystemInformation)(
	ULONG SystemInformationClass,
	PVOID SystemInformation,
	ULONG SystemInformationLength,
	PULONG ReturnLength
	);

typedef NTSTATUS(NTAPI *_NtQueryObject)(
	HANDLE   Handle,
	ULONG    ObjectInformationClass,
	PVOID    ObjectInformation,
	ULONG    ObjectInformationLength,
	PULONG   ReturnLength
	);

typedef struct _ML_PROCESS {
	DWORD id;
	HANDLE handle;
}ML_PROCESS;


__declspec(dllexport) BOOL KillGWMutex(void)
{
	_NtQuerySystemInformation queryinfo = (_NtQuerySystemInformation)GetProcAddress(GetModuleHandleA("ntdll.dll"), "NtQuerySystemInformation");
	_NtQueryObject queryobj = (_NtQueryObject)GetProcAddress(GetModuleHandleA("ntdll.dll"), "NtQueryObject");

	WCHAR name[0x200];
	BYTE buffer[0x1000];
	PROCESSENTRY32 procinfo;
	procinfo.dwSize = sizeof(PROCESSENTRY32);
	POBJECT_BASIC_INFORMATION obj = (POBJECT_BASIC_INFORMATION)buffer;
	DWORD required_len = 0x1000;
	PSYSTEM_HANDLE_INFORMATION handleinfo = (PSYSTEM_HANDLE_INFORMATION)malloc(required_len);


	while (queryinfo(0x10, handleinfo, required_len, &required_len) == 0xC0000004) {
		free(handleinfo);
		handleinfo = (PSYSTEM_HANDLE_INFORMATION)malloc(required_len);
	}

	HANDLE hSnappy = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

	BOOL result = Process32First(hSnappy, &procinfo);
	if (!result) {
		free(handleinfo);
		return FALSE;
	}

	do
	{

		if (wcscmp(procinfo.szExeFile, TEXT("Gw.exe"))) {
			continue;
		}

		HANDLE hProc = OpenProcess(PROCESS_ALL_ACCESS, FALSE, procinfo.th32ProcessID);

		if (!hProc) {
			continue;
		}


		for (ULONG i = 0; i < handleinfo->HandleCount; ++i)
		{
			SYSTEM_HANDLE cur = handleinfo->Handles[i];
			if (cur.dwProcessId == procinfo.th32ProcessID && cur.GrantedAccess == MUTEX_ALL_ACCESS) {
				HANDLE hDuplicate = NULL;
				DuplicateHandle(hProc, (HANDLE)cur.wValue, GetCurrentProcess(), &hDuplicate, 0, FALSE, DUPLICATE_SAME_ACCESS);
				queryobj(hDuplicate, 0, obj, sizeof(OBJECT_BASIC_INFORMATION), NULL);

				if (obj->NameInformationLength) {
					PUNICODE_STRING mutexname = (PUNICODE_STRING)name;
					queryobj(hDuplicate, 1, mutexname, 0x200 * sizeof(WCHAR), NULL);

					WCHAR* idx = (WCHAR*)mutexname->Buffer + mutexname->Length / sizeof(WCHAR);
					while (*(--idx) != L'\\');

					if (!memcmp(idx, L"\\AN-Mutex", sizeof(L"\\AN-Mutex") - sizeof(WCHAR))) {
						DuplicateHandle(hProc, (HANDLE)cur.wValue, NULL, NULL, 0, FALSE, DUPLICATE_CLOSE_SOURCE);
						CloseHandle(hDuplicate);
						break;
					}
				}
				CloseHandle(hDuplicate);
			}
		}

	} while (Process32Next(hSnappy, &procinfo));

	CloseHandle(hSnappy);
	free(handleinfo);
	return TRUE;
}
