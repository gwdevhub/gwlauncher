#include "GWML.h"


__declspec(dllexport) BOOL PatchDAT(HANDLE hProcess)
{
  const BYTE sig_DatOpenFile[] = { 0x6A, 0x00, 0xBA, 0x00, 0x00, 0x00, 0xC0 };
  const BYTE sig_WriteFilePatch[] = { 0x6A, 0x00, 0x52, 0x57, 0x50, 0x51 };

  BYTE* buffer = malloc(0x49a000);
  BYTE* DatOpenFile = NULL;
  BYTE* WriteFilePatch = NULL;

  if(!ReadProcessMemory(hProcess,(void*)0x401000,buffer,0x49a000,NULL)){
    free(buffer);
    return FALSE;
  }

  for(DWORD i = 0; i < 0x49a000; ++i){
    if(!memcmp(buffer + i,sig_DatOpenFile,sizeof(sig_DatOpenFile)))
        DatOpenFile = (BYTE*)(0x401000 + i + 1);
    if(!memcmp(buffer + i,sig_WriteFilePatch,sizeof(sig_WriteFilePatch)))
        WriteFilePatch = (BYTE*)(0x401000 + i);
    if(DatOpenFile && WriteFilePatch)
      break;
  }

  buffer[0] = 0x03;

  if(!WriteProcessMemory(hProcess,DatOpenFile,buffer,1,NULL)){
    free(buffer);
    return FALSE;
  }

  memset(buffer,0x90,12);

  if(!WriteProcessMemory(hProcess,WriteFilePatch,buffer,12,NULL)){
    free(buffer);
    return FALSE;
  }

  free(buffer);
  return TRUE;
}
