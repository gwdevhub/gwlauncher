#include <Windows.h>
#include <stdio.h>

#define GWML_NO_DATFIX				1
#define GWML_KEEP_SUSPENDED 		2

#define MCERROR(msg) { printf("ERROR: " msg "\n"); return FALSE; }

#define ASSERT(action) if(!( action )) MCERROR(#action)


BYTE g__gwdata[0x49a000];

__declspec(dllexport) BOOL MCPatch(HANDLE hProcess) {
	const BYTE sig_patch[] = { 0x55, 0x8B, 0xEC, 0x81, 0xEC, 0x2C, 0x01, 0x00,
							   0x00, 0x53, 0x56, 0x8B, 0xDA, 0x8B, 0xF1, 0x57 };

	BYTE* mcpatch = NULL;

	if (!ReadProcessMemory(hProcess, (void*)0x401000, g__gwdata, 0x49a000, NULL)) {
		return FALSE;
	}

	for (DWORD i = 0; i < 0x49a000; ++i) {
		if (!memcmp(g__gwdata + i, sig_patch, sizeof(sig_patch))) {
			mcpatch = (BYTE*)(0x401000 + i);
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
			mov		edx, 0x80000000
			mov		dword ptr ss : [esp + 0x4], 3
			mov		dword ptr ss : [esp + 0x10], 1
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
	const BYTE sig_datfix[] = { 0x56, 0x85, 0xC0, 0x8B, 0xF2, 0x74, 0x3B };
	BYTE jmpencoding[5] = { 0xE9, 0, 0, 0, 0 };

	BYTE* datfix = NULL;

	for (DWORD i = 0; i < 0x49a000; ++i) {
		if (!memcmp(g__gwdata + i, sig_datfix, sizeof(sig_datfix))) {
			datfix = (BYTE*)(0x401000 + i - 0xE);
			break;
		}	
	}
	
	ASSERT(datfix);
	
	printf("datfix = %X\n",datfix);
	void* asmbuffer = VirtualAllocEx(hProcess,NULL,
									(uintptr_t)stubend_patchdat - (uintptr_t)stub_patchdat,
									MEM_COMMIT | MEM_RESERVE, 
									PAGE_EXECUTE_READWRITE);
									
	DWORD oldprot;
	ASSERT(VirtualProtect(stub_patchdat,(uintptr_t)stubend_patchdat - (uintptr_t)stub_patchdat,PAGE_EXECUTE_READWRITE,&oldprot));	
	ASSERT(WriteProcessMemory(hProcess, asmbuffer, (void*)stub_patchdat, (uintptr_t)stubend_patchdat - (uintptr_t)stub_patchdat + 5, NULL));
	*(DWORD*)(jmpencoding + 1) = ENCODE_REL((uintptr_t)asmbuffer + ((uintptr_t)stubend_patchdat - (uintptr_t)stub_patchdat),(uintptr_t)datfix + 9);
	ASSERT(WriteProcessMemory(hProcess, (void*)((uintptr_t)asmbuffer + ((uintptr_t)stubend_patchdat - (uintptr_t)stub_patchdat)), jmpencoding, sizeof(jmpencoding), NULL));
	*(DWORD*)(jmpencoding + 1) = ENCODE_REL(datfix,asmbuffer);
	ASSERT(WriteProcessMemory(hProcess, datfix, jmpencoding, sizeof(jmpencoding), NULL));
	
	return TRUE;
}


__declspec(dllexport) DWORD LaunchClient(LPCWSTR path,LPCWSTR args, DWORD flags,DWORD* out_hThread)
{
	WCHAR commandLine[0x100];
	swprintf(commandLine, 0x100, L"\"%s\" %s", path, args);
	
	
	STARTUPINFOW startinfo				= { 0 };
    PROCESS_INFORMATION procinfo 	= { 0 };


    if(!CreateProcessW(NULL,commandLine,NULL,NULL,FALSE,CREATE_SUSPENDED,NULL,NULL,&startinfo,&procinfo))
        MCERROR("CreateProcessW");
	
	if(!MCPatch(procinfo.hProcess))
       MCERROR("MCPatch");
	
	if((flags & GWML_NO_DATFIX) == 0)
		if(!DATFix(procinfo.hProcess))
			 MCERROR("DATFix");
		
	if((flags & GWML_KEEP_SUSPENDED) == 0){
		ResumeThread(procinfo.hThread);
		CloseHandle(procinfo.hThread);
	}
	else 
		if (out_hThread != NULL)
			*out_hThread = (DWORD)procinfo.hThread;
	
	CloseHandle(procinfo.hProcess);
	return procinfo.dwProcessId;
}