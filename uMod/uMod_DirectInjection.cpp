/*
This file is part of Universal Modding Engine.


Universal Modding Engine is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Universal Modding Engine is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Universal Modding Engine.  If not, see <http://www.gnu.org/licenses/>.
*/


/*
 *
 * Drew_Benton
 * http://www.codeproject.com/KB/threads/completeinject.aspx
 *
 */

#include "uMod_Main.h"
 /***************************************************************************************************/
 //	Function: 
 //		Inject
 //	
 //	Parameters:
 //		HANDLE hProcess - The handle to the process to inject the DLL into.
 //
 //		const char* dllname - The name of the DLL to inject into the process.
 //		
 //		const char* funcname - The name of the function to call once the DLL has been injected.
 //
 //	Description:
 //		This function will inject a DLL into a process and execute an exported function
 //		from the DLL to "initialize" it. The function should be in the format shown below,
 //		not parameters and no return type. Do not forget to prefix extern "C" if you are in C++
 //
 //			__declspec(dllexport) void FunctionName(void)
 //
 //		The function that is called in the injected DLL
 //		-MUST- return, the loader waits for the thread to terminate before removing the 
 //		allocated space and returning control to the Loader. This method of DLL injection
 //		also adds error handling, so the end user knows if something went wrong.
 /***************************************************************************************************/

void Inject(HANDLE hProcess, const wchar_t* dllname, const char* funcname)
{
	//------------------------------------------//
	// Function variables.						//
	//------------------------------------------//

		// Main DLL we will need to load
	HMODULE kernel32 = NULL;

	// Main functions we will need to import
	FARPROC loadlibrary = NULL;
	FARPROC getprocaddress = NULL;
	FARPROC exitprocess = NULL;
	FARPROC exitthread = NULL;
	FARPROC freelibraryandexitthread = NULL;

	// The workspace we will build the codecave on locally
	LPBYTE workspace = NULL;
	DWORD workspaceIndex = 0;

	// The memory in the process we write to
	LPVOID codecaveAddress = NULL;
	DWORD dwCodecaveAddress = 0;

	// Strings we have to write into the process
	char injectDllName[MAX_PATH + 1] = { 0 };
	char injectFuncName[MAX_PATH + 1] = { 0 };
	char injectError0[MAX_PATH + 1] = { 0 };
	char injectError1[MAX_PATH + 1] = { 0 };
	char injectError2[MAX_PATH + 1] = { 0 };
	char user32Name[MAX_PATH + 1] = { 0 };
	char msgboxName[MAX_PATH + 1] = { 0 };

	// Placeholder addresses to use the strings
	DWORD user32NameAddr = 0;
	DWORD user32Addr = 0;
	DWORD msgboxNameAddr = 0;
	DWORD msgboxAddr = 0;
	DWORD dllAddr = 0;
	DWORD dllNameAddr = 0;
	DWORD funcNameAddr = 0;
	DWORD error0Addr = 0;
	DWORD error1Addr = 0;
	DWORD error2Addr = 0;

	// Where the codecave execution should begin at
	DWORD codecaveExecAddr = 0;

	// Handle to the thread we create in the process
	HANDLE hThread = NULL;

	// Temp variables
	DWORD dwTmpSize = 0;

	// Old protection on page we are writing to in the process and the bytes written
	DWORD oldProtect = 0;
	DWORD bytesRet = 0;

	//------------------------------------------//
	// Variable initialization.					//
	//------------------------------------------//

		// Get the address of the main DLL
	kernel32 = LoadLibraryW(L"kernel32.dll");

	// Get our functions
	loadlibrary = GetProcAddress(kernel32, "LoadLibraryA");
	getprocaddress = GetProcAddress(kernel32, "GetProcAddress");
	exitprocess = GetProcAddress(kernel32, "ExitProcess");
	exitthread = GetProcAddress(kernel32, "ExitThread");
	freelibraryandexitthread = GetProcAddress(kernel32, "FreeLibraryAndExitThread");

	// This section will cause compiler warnings on VS8, 
	// you can upgrade the functions or ignore them

		// Build names
	_snprintf(injectDllName, MAX_PATH, "%ls", dllname);
	_snprintf(injectFuncName, MAX_PATH, "%s", funcname);
	_snprintf(user32Name, MAX_PATH, "user32.dll");
	_snprintf(msgboxName, MAX_PATH, "MessageBoxA");

	// Build error messages
	_snprintf(injectError0, MAX_PATH, "Error");
	_snprintf(injectError1, MAX_PATH, "Could not load the dll: %s", injectDllName);
	_snprintf(injectError2, MAX_PATH, "Could not load the function: %s", injectFuncName);

	// Create the workspace
	workspace = (LPBYTE)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, 1024);

	// Allocate space for the codecave in the process
	codecaveAddress = VirtualAllocEx(hProcess, 0, 1024, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
	dwCodecaveAddress = PtrToUlong(codecaveAddress);

	// Note there is no error checking done above for any functions that return a pointer/handle.
	// I could have added them, but it'd just add more messiness to the code and not provide any real
	// benefit. It's up to you though in your final code if you want it there or not.

	//------------------------------------------//
	// Data and string writing.					//
	//------------------------------------------//

		// Write out the address for the user32 dll address
	user32Addr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = 0;
	memcpy(workspace + workspaceIndex, &dwTmpSize, 4);
	workspaceIndex += 4;

	// Write out the address for the MessageBoxA address
	msgboxAddr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = 0;
	memcpy(workspace + workspaceIndex, &dwTmpSize, 4);
	workspaceIndex += 4;

	// Write out the address for the injected DLL's module
	dllAddr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = 0;
	memcpy(workspace + workspaceIndex, &dwTmpSize, 4);
	workspaceIndex += 4;

	// User32 Dll Name
	user32NameAddr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = (DWORD)strlen(user32Name) + 1;
	memcpy(workspace + workspaceIndex, user32Name, dwTmpSize);
	workspaceIndex += dwTmpSize;

	// MessageBoxA name
	msgboxNameAddr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = (DWORD)strlen(msgboxName) + 1;
	memcpy(workspace + workspaceIndex, msgboxName, dwTmpSize);
	workspaceIndex += dwTmpSize;

	// Dll Name
	dllNameAddr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = (DWORD)strlen(injectDllName) + 1;
	memcpy(workspace + workspaceIndex, injectDllName, dwTmpSize);
	workspaceIndex += dwTmpSize;

	// Function Name
	funcNameAddr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = (DWORD)strlen(injectFuncName) + 1;
	memcpy(workspace + workspaceIndex, injectFuncName, dwTmpSize);
	workspaceIndex += dwTmpSize;

	// Error Message 1
	error0Addr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = (DWORD)strlen(injectError0) + 1;
	memcpy(workspace + workspaceIndex, injectError0, dwTmpSize);
	workspaceIndex += dwTmpSize;

	// Error Message 2
	error1Addr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = (DWORD)strlen(injectError1) + 1;
	memcpy(workspace + workspaceIndex, injectError1, dwTmpSize);
	workspaceIndex += dwTmpSize;


	// Error Message 3
	error2Addr = workspaceIndex + dwCodecaveAddress;
	dwTmpSize = (DWORD)strlen(injectError2) + 1;
	memcpy(workspace + workspaceIndex, injectError2, dwTmpSize);
	workspaceIndex += dwTmpSize;

	// Pad a few INT3s after string data is written for seperation
	workspace[workspaceIndex++] = 0xCC;
	workspace[workspaceIndex++] = 0xCC;
	workspace[workspaceIndex++] = 0xCC;

	// Store where the codecave execution should begin
	codecaveExecAddr = workspaceIndex + dwCodecaveAddress;

	// For debugging - infinite loop, attach onto process and step over
		//workspace[workspaceIndex++] = 0xEB;
		//workspace[workspaceIndex++] = 0xFE;

	//------------------------------------------//
	// User32.dll loading.						//
	//------------------------------------------//

	// User32 DLL Loading
		// PUSH 0x00000000 - Push the address of the DLL name to use in LoadLibraryA
	workspace[workspaceIndex++] = 0x68;
	memcpy(workspace + workspaceIndex, &user32NameAddr, 4);
	workspaceIndex += 4;

	// MOV EAX, ADDRESS - Move the address of LoadLibraryA into EAX
	workspace[workspaceIndex++] = 0xB8;
	memcpy(workspace + workspaceIndex, &loadlibrary, 4);
	workspaceIndex += 4;

	// CALL EAX - Call LoadLibraryA
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;

	// MessageBoxA Loading
		// PUSH 0x000000 - Push the address of the function name to load
	workspace[workspaceIndex++] = 0x68;
	memcpy(workspace + workspaceIndex, &msgboxNameAddr, 4);
	workspaceIndex += 4;

	// Push EAX, module to use in GetProcAddress
	workspace[workspaceIndex++] = 0x50;

	// MOV EAX, ADDRESS - Move the address of GetProcAddress into EAX
	workspace[workspaceIndex++] = 0xB8;
	memcpy(workspace + workspaceIndex, &getprocaddress, 4);
	workspaceIndex += 4;

	// CALL EAX - Call GetProcAddress
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;

	// MOV [ADDRESS], EAX - Save the address to our variable
	workspace[workspaceIndex++] = 0xA3;
	memcpy(workspace + workspaceIndex, &msgboxAddr, 4);
	workspaceIndex += 4;

	//------------------------------------------//
	// Injected dll loading.					//
	//------------------------------------------//

	/*
		// This is the way the following assembly code would look like in C/C++

		// Load the injected DLL into this process
		HMODULE h = LoadLibrary("mydll.dll");
		if(!h)
		{
			MessageBox(0, "Could not load the dll: mydll.dll", "Error", MB_ICONERROR);
			ExitProcess(0);
		}

		// Get the address of the export function
		FARPROC p = GetProcAddress(h, "Initialize");
		if(!p)
		{
			MessageBox(0, "Could not load the function: Initialize", "Error", MB_ICONERROR);
			ExitProcess(0);
		}

		// So we do not need a function pointer interface
		__asm call p

		// Exit the thread so the loader continues
		ExitThread(0);
	*/

	// DLL Loading
		// PUSH 0x00000000 - Push the address of the DLL name to use in LoadLibraryA
	workspace[workspaceIndex++] = 0x68;
	memcpy(workspace + workspaceIndex, &dllNameAddr, 4);
	workspaceIndex += 4;

	// MOV EAX, ADDRESS - Move the address of LoadLibraryA into EAX
	workspace[workspaceIndex++] = 0xB8;
	memcpy(workspace + workspaceIndex, &loadlibrary, 4);
	workspaceIndex += 4;

	// CALL EAX - Call LoadLibraryA
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;

	// Error Checking
		// CMP EAX, 0
	workspace[workspaceIndex++] = 0x83;
	workspace[workspaceIndex++] = 0xF8;
	workspace[workspaceIndex++] = 0x00;

	// JNZ EIP + 0x1E to skip over eror code
	workspace[workspaceIndex++] = 0x75;
	workspace[workspaceIndex++] = 0x1E;

	// Error Code 1
		// MessageBox
			// PUSH 0x10 (MB_ICONHAND)
	workspace[workspaceIndex++] = 0x6A;
	workspace[workspaceIndex++] = 0x10;

	// PUSH 0x000000 - Push the address of the MessageBox title
	workspace[workspaceIndex++] = 0x68;
	memcpy(workspace + workspaceIndex, &error0Addr, 4);
	workspaceIndex += 4;

	// PUSH 0x000000 - Push the address of the MessageBox message
	workspace[workspaceIndex++] = 0x68;
	memcpy(workspace + workspaceIndex, &error1Addr, 4);
	workspaceIndex += 4;

	// Push 0
	workspace[workspaceIndex++] = 0x6A;
	workspace[workspaceIndex++] = 0x00;

	// MOV EAX, [ADDRESS] - Move the address of MessageBoxA into EAX
	workspace[workspaceIndex++] = 0xA1;
	memcpy(workspace + workspaceIndex, &msgboxAddr, 4);
	workspaceIndex += 4;

	// CALL EAX - Call MessageBoxA
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;

	// ExitProcess
		// Push 0
	workspace[workspaceIndex++] = 0x6A;
	workspace[workspaceIndex++] = 0x00;

	// MOV EAX, ADDRESS - Move the address of ExitProcess into EAX
	workspace[workspaceIndex++] = 0xB8;
	memcpy(workspace + workspaceIndex, &exitprocess, 4);
	workspaceIndex += 4;

	// CALL EAX - Call MessageBoxA
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;

	//	Now we have the address of the injected DLL, so save the handle

		// MOV [ADDRESS], EAX - Save the address to our variable
	workspace[workspaceIndex++] = 0xA3;
	memcpy(workspace + workspaceIndex, &dllAddr, 4);
	workspaceIndex += 4;

	// Load the initilize function from it

		// PUSH 0x000000 - Push the address of the function name to load
	workspace[workspaceIndex++] = 0x68;
	memcpy(workspace + workspaceIndex, &funcNameAddr, 4);
	workspaceIndex += 4;

	// Push EAX, module to use in GetProcAddress
	workspace[workspaceIndex++] = 0x50;

	// MOV EAX, ADDRESS - Move the address of GetProcAddress into EAX
	workspace[workspaceIndex++] = 0xB8;
	memcpy(workspace + workspaceIndex, &getprocaddress, 4);
	workspaceIndex += 4;

	// CALL EAX - Call GetProcAddress
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;

	// Error Checking
		// CMP EAX, 0
	workspace[workspaceIndex++] = 0x83;
	workspace[workspaceIndex++] = 0xF8;
	workspace[workspaceIndex++] = 0x00;

	// JNZ EIP + 0x1C to skip eror code
	workspace[workspaceIndex++] = 0x75;
	workspace[workspaceIndex++] = 0x1C;

	// Error Code 2
		// MessageBox
			// PUSH 0x10 (MB_ICONHAND)
	workspace[workspaceIndex++] = 0x6A;
	workspace[workspaceIndex++] = 0x10;

	// PUSH 0x000000 - Push the address of the MessageBox title
	workspace[workspaceIndex++] = 0x68;
	memcpy(workspace + workspaceIndex, &error0Addr, 4);
	workspaceIndex += 4;

	// PUSH 0x000000 - Push the address of the MessageBox message
	workspace[workspaceIndex++] = 0x68;
	memcpy(workspace + workspaceIndex, &error2Addr, 4);
	workspaceIndex += 4;

	// Push 0
	workspace[workspaceIndex++] = 0x6A;
	workspace[workspaceIndex++] = 0x00;

	// MOV EAX, ADDRESS - Move the address of MessageBoxA into EAX
	workspace[workspaceIndex++] = 0xA1;
	memcpy(workspace + workspaceIndex, &msgboxAddr, 4);
	workspaceIndex += 4;

	// CALL EAX - Call MessageBoxA
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;

	// ExitProcess
		// Push 0
	workspace[workspaceIndex++] = 0x6A;
	workspace[workspaceIndex++] = 0x00;

	// MOV EAX, ADDRESS - Move the address of ExitProcess into EAX
	workspace[workspaceIndex++] = 0xB8;
	memcpy(workspace + workspaceIndex, &exitprocess, 4);
	workspaceIndex += 4;

	//	Now that we have the address of the function, we cam call it, 
	// if there was an error, the messagebox would be called as well.

		// CALL EAX - Call ExitProcess -or- the Initialize function
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;

	// If we get here, the Initialize function has been called, 
	// so it's time to close this thread and optionally unload the DLL.

//------------------------------------------//
// Exiting from the injected dll.			//
//------------------------------------------//

// Call ExitThread to leave the DLL loaded
#if 1
	// Push 0 (exit code)
	workspace[workspaceIndex++] = 0x6A;
	workspace[workspaceIndex++] = 0x00;

	// MOV EAX, ADDRESS - Move the address of ExitThread into EAX
	workspace[workspaceIndex++] = 0xB8;
	memcpy(workspace + workspaceIndex, &exitthread, 4);
	workspaceIndex += 4;

	// CALL EAX - Call ExitThread
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;
#endif

	// Call FreeLibraryAndExitThread to unload DLL
#if 0
	// Push 0 (exit code)
	workspace[workspaceIndex++] = 0x6A;
	workspace[workspaceIndex++] = 0x00;

	// PUSH [0x000000] - Push the address of the DLL module to unload
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0x35;
	memcpy(workspace + workspaceIndex, &dllAddr, 4);
	workspaceIndex += 4;

	// MOV EAX, ADDRESS - Move the address of FreeLibraryAndExitThread into EAX
	workspace[workspaceIndex++] = 0xB8;
	memcpy(workspace + workspaceIndex, &freelibraryandexitthread, 4);
	workspaceIndex += 4;

	// CALL EAX - Call FreeLibraryAndExitThread
	workspace[workspaceIndex++] = 0xFF;
	workspace[workspaceIndex++] = 0xD0;
#endif

	//------------------------------------------//
	// Code injection and cleanup.				//
	//------------------------------------------//

		// Change page protection so we can write executable code
	VirtualProtectEx(hProcess, codecaveAddress, workspaceIndex, PAGE_EXECUTE_READWRITE, &oldProtect);

	// Write out the patch
	WriteProcessMemory(hProcess, codecaveAddress, workspace, workspaceIndex, &bytesRet);

	// Restore page protection
	VirtualProtectEx(hProcess, codecaveAddress, workspaceIndex, oldProtect, &oldProtect);

	// Make sure our changes are written right away
	FlushInstructionCache(hProcess, codecaveAddress, workspaceIndex);

	// Free the workspace memory
	HeapFree(GetProcessHeap(), 0, workspace);

	// Execute the thread now and wait for it to exit, note we execute where the code starts, and not the codecave start
	// (since we wrote strings at the start of the codecave) -- NOTE: void* used for VC6 compatibility instead of UlongToPtr
	hThread = CreateRemoteThread(hProcess, NULL, 0, (LPTHREAD_START_ROUTINE)((void*)codecaveExecAddr), 0, 0, NULL);
	WaitForSingleObject(hThread, INFINITE);

	// Free the memory in the process that we allocated
	VirtualFreeEx(hProcess, codecaveAddress, 0, MEM_RELEASE);
}
