#ifndef _GWML_H
#define _GWML_H

#include <Windows.h>

#define GWML_NO_DATFIX        1
#define GWML_KEEP_SUSPENDED   2
#define GWML_NO_LOGIN		  4

__declspec(dllexport) BOOL   KillGWMutex(void);
__declspec(dllexport) BOOL   PatchDAT(HANDLE hProcess);

__declspec(dllexport) DWORD  LaunchClient(LPCWSTR clientpath,LPCWSTR email, LPCWSTR password,LPCWSTR character, LPCWSTR extra_args, DWORD flags, DWORD* out_hThread);

#endif /* end of include guard: _GWML_H */
