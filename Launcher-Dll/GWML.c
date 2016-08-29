#include "GWML.h"
#include <stdio.h>

__declspec(dllexport) DWORD LaunchClient(LPCWSTR clientpath,LPCWSTR email, LPCWSTR password,LPCWSTR character,LPCWSTR extra_args, DWORD flags, DWORD* out_hThread)
{
    WCHAR commandLine[0x100];

	if (flags & GWML_NO_LOGIN > 0) {
		swprintf(commandLine, 0x100, L"\"%s\" %s", clientpath, extra_args);
	}
	else {
		swprintf(commandLine, 0x100, L"\"%s\" -email \"%s\" -password \"%s\" -character \"%s\" %s", clientpath, email, password, character, extra_args);
	}
   

    STARTUPINFOW startinfo;
    PROCESS_INFORMATION procinfo;

    memset(&procinfo,0,sizeof(PROCESS_INFORMATION));
    memset(&startinfo,0,sizeof(STARTUPINFO));

    if(!CreateProcessW(NULL,commandLine,NULL,NULL,FALSE,CREATE_SUSPENDED,NULL,NULL,&startinfo,&procinfo))
        return 0;


    if(!KillGWMutex())
        return 0;

    if(!(flags & GWML_NO_DATFIX > 0))
      if(!PatchDAT(procinfo.hProcess))
        return 0;

	if(!(flags & GWML_KEEP_SUSPENDED > 0))
		ResumeThread(procinfo.hThread);
  else
    if(out_hThread != NULL)
      *out_hThread = (DWORD)procinfo.hThread;

    return procinfo.dwProcessId;
}




BOOL WINAPI DllMain(HMODULE hModule,DWORD dwReason,LPVOID lpReserved){ return TRUE; }
