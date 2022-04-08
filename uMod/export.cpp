#include "uMod_Main.h"

static uMod_Frame* frame = nullptr;
static uMod_GamePage* page = nullptr;

__declspec(dllexport) int RunServer() {
	uMod_Settings set;
	set.Load();

	Language = new uMod_Language("English");
	HANDLE CheckForSingleRun = CreateMutex(NULL, true, L"Global\\uMod_CheckForSingleRun_GWML");
	if (ERROR_ALREADY_EXISTS == GetLastError())
	{
		//wxMessageBox(Language->Error_AlreadyRunning, "ERROR", wxOK | wxICON_ERROR);
		return false;
	}
	frame = new uMod_Frame(uMod_VERSION, set);

	return true;
}

__declspec(dllexport) bool LoadTextures(LPCWSTR path) {
	if (frame->Notebook->GetPageCount() == 0) return false;
	uMod_GamePage* page = (uMod_GamePage*)frame->Notebook->GetPage(0);
	if (page == NULL) return false;

	//wxString file_name = wxFileSelector( Language->ChooseFile, page->GetOpenPath(), "", "*.*",  "textures (*.dds)|*.dds|zip (*.zip)|*.zip|tpf (*.tpf)|*.tpf", wxFD_OPEN | wxFD_FILE_MUST_EXIST, this);
	wxString file_name = path;
	if (!file_name.empty())
	{
		page->SetOpenPath(file_name.BeforeLast('/'));
		if (page->AddTexture(file_name))
		{
			//wxMessageBox(page->LastError, "ERROR", wxOK | wxICON_ERROR);
			page->LastError.Empty();
		}
	}
	return true;
}