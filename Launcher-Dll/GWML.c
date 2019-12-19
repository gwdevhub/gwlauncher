#include <Windows.h>
#include <stdio.h>

#define GWML_NO_DATFIX				1
#define GWML_KEEP_SUSPENDED 		2

#define MCERROR(msg) do { printf("ERROR: " msg "\n"); FreeConsole(); return FALSE; } while(0)
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
	
	printf("mcpatch = %X\n",mcpatch);

	const BYTE payload[] = { 0x31, 0xC0, 0x90, 0xC3 };

	if (!WriteProcessMemory(hProcess, mcpatch, payload, sizeof(payload), NULL))
		MCERROR("WriteProcessMemory mcpatch");

	return TRUE;
}

__declspec(naked) void stub_patchdat(void)
{
	__asm {
		push	ecx
		push	edx
		push	eax
		mov		ecx, dword ptr ds:[esp + 0x10]
		xor		edx,edx
		xor		eax,eax

		getlen:
			mov		ax, word ptr ds:[ecx + edx]
			cmp		ax, 0
			je			getbackslash
			add		edx, 2
			jmp		getlen

		getbackslash:
			sub		edx, 2
			mov		ax, word ptr ds : [ecx + edx]
			cmp		ax, L'\\'
			je			checkifdat
			jmp		getbackslash

		checkifdat:
			add		edx, 2
			mov		ax, word ptr ds : [ecx + edx]
			cmp		ax, L'G'
			jne		exitwithoutset
			add		edx, 2
			mov		ax, word ptr ds : [ecx + edx]
			cmp		ax, L'w'
			jne		exitwithoutset
			add		edx, 2
			mov		ax, word ptr ds : [ecx + edx]
			cmp		ax, L'.'
			jne		exitwithoutset
			add		edx, 2
			mov		ax, word ptr ds : [ecx + edx]
			cmp		ax, L'd'
			jne		exitwithoutset
			add		edx, 2
			mov		ax, word ptr ds : [ecx + edx]
			cmp		ax, L'a'
			jne		exitwithoutset
			add		edx, 2
			mov		ax, word ptr ds : [ecx + edx]
			cmp		ax, L't'
			jne		exitwithoutset

		setpermissions:
			pop		eax
			pop		edx
			pop		ecx
			mov		dword ptr ss : [esp + 0x8], 0x80000000
			mov		dword ptr ss : [esp + 0xC], 3
			mov		dword ptr ss : [esp + 0x18], 1
			jmp		end

		exitwithoutset:
			pop		eax
			pop		edx
			pop		ecx

		end:
			push	ebp
			mov		ebp, esp
			sub		esp, 0x104
	}
}
__declspec(naked) void stubend_patchdat(void) {}


#define ENCODE_REL(from,to) (uintptr_t)((uintptr_t)(to) - (uintptr_t)(from) - 5)

__declspec(dllexport) BOOL DATFix(HANDLE hProcess)
{
	const BYTE sig_datfix[] = { 0x8B, 0x4D, 0x18, 0x8B, 0x55, 0x1C, 0x8B};
	BYTE jmpencoding[5] = { 0xE9, 0, 0, 0, 0 };

	BYTE* datfix = NULL;

	for (DWORD i = 0; i < 0x48D000; ++i) {
		if (!memcmp(g_gwdata + i, sig_datfix, sizeof(sig_datfix))) {
			datfix = (BYTE*)(g_moduleBase + i - 0x1A);
			break;
		}	
	}
	
	void* asmbuffer = VirtualAllocEx(hProcess,NULL,
									(uintptr_t)stubend_patchdat - (uintptr_t)stub_patchdat + 0x20,
									MEM_COMMIT | MEM_RESERVE, 
									PAGE_EXECUTE_READWRITE);


									
	DWORD oldprot;
	ASSERT(VirtualProtect(stub_patchdat,(uintptr_t)stubend_patchdat - (uintptr_t)stub_patchdat,PAGE_EXECUTE_READWRITE,&oldprot));	

	// dump detour into internal mem
	ASSERT(WriteProcessMemory(hProcess, asmbuffer, (void*)stub_patchdat, (uintptr_t)stubend_patchdat - (uintptr_t)stub_patchdat, NULL));

	// write return to ofunc
	BYTE* asmend = (BYTE*)stubend_patchdat - 1;
	while (*asmend == 0xCC /* INT3 */)
		asmend--;
	asmend++;
	asmend -= (uintptr_t)stub_patchdat;
	asmend += (uintptr_t)asmbuffer;
	printf("asmend = %X\n", asmend);
	

	*(DWORD*)(jmpencoding + 1) = ENCODE_REL(asmend,(uintptr_t)datfix + 9);
	ASSERT(WriteProcessMemory(hProcess, (void*)asmend, jmpencoding, sizeof(jmpencoding), NULL));

	// write jmp to detour
	*(DWORD*)(jmpencoding + 1) = ENCODE_REL(datfix,asmbuffer);
	ASSERT(WriteProcessMemory(hProcess, datfix, jmpencoding, sizeof(jmpencoding), NULL));
	
	return TRUE;
}


__declspec(dllexport) DWORD LaunchClient(LPCWSTR path,LPCWSTR args, DWORD flags,DWORD* out_hThread)
{
#if 0
	AllocConsole();
	freopen("CONOUT$", "w", stdout);
#endif

	WCHAR commandLine[0x100];
	swprintf(commandLine, 0x100, L"\"%s\" %s", path, args);
	
	
	STARTUPINFOW startinfo = { 0 };
    PROCESS_INFORMATION procinfo = { 0 };


    if(!CreateProcessW(NULL,commandLine,NULL,NULL,FALSE,CREATE_SUSPENDED,NULL,NULL,&startinfo,&procinfo))
        MCERROR("CreateProcessW");

	g_moduleBase = GetProcessModuleBase(procinfo.hProcess);
	
	if(!MCPatch(procinfo.hProcess))
       MCERROR("MCPatch");
	
	if((flags & GWML_NO_DATFIX) == 0)
		if(!DATFix(procinfo.hProcess))
			 MCERROR("DATFix");
		
	if(0) {//(flags & GWML_KEEP_SUSPENDED) == 0){
		ResumeThread(procinfo.hThread);
		CloseHandle(procinfo.hThread);
	}
	else 
		if (out_hThread != NULL)
			*out_hThread = (DWORD)procinfo.hThread;
	
	CloseHandle(procinfo.hProcess);
	return procinfo.dwProcessId;
}