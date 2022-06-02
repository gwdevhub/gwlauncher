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



#ifndef uMod_GUI_H_
#define uMod_GUI_H_


#include "uMod_Main.h"

class uMod_Frame : public wxFrame
{
public:
	uMod_Frame(const wxString& title, uMod_Settings& set);
	~uMod_Frame(void);



	void OnAddGame(wxCommandEvent& event);
	void OnDeleteGame(wxCommandEvent& event);

	void OnClose(wxCloseEvent& WXUNUSED(event));


	void OnButtonOpen(wxCommandEvent& WXUNUSED(event));
	void OnButtonPath(wxCommandEvent& WXUNUSED(event));
	void OnButtonUpdate(wxCommandEvent& WXUNUSED(event));
	void OnButtonReload(wxCommandEvent& WXUNUSED(event));

	void OnMenuStartGame(wxCommandEvent& event);

	void OnMenuUseHook(wxCommandEvent& event);
	void OnMenuAddGame(wxCommandEvent& WXUNUSED(event));
	void OnMenuDeleteGame(wxCommandEvent& WXUNUSED(event));

	void OnMenuOpenTemplate(wxCommandEvent& WXUNUSED(event));
	void OnMenuSaveTemplate(wxCommandEvent& WXUNUSED(event));
	void OnMenuSaveTemplateAs(wxCommandEvent& WXUNUSED(event));
	void OnMenuSetDefaultTemplate(wxCommandEvent& WXUNUSED(event));
	void OnMenuLanguage(wxCommandEvent& WXUNUSED(event));

	void OnMenuExit(wxCommandEvent& WXUNUSED(event));

	void OnMenuHelp(wxCommandEvent& WXUNUSED(event));
	void OnMenuAbout(wxCommandEvent& WXUNUSED(event));
	void OnMenuAcknowledgement(wxCommandEvent& WXUNUSED(event));

private:

	int ActivateGamesControl(void);
	int DeactivateGamesControl(void);

	uMod_Settings Settings;
	int KillServer(void);
	int GetHookedGames(wxArrayString& array);
	int SetHookedGames(const wxArrayString& array);

	int GetInjectedGames(wxArrayString& games, wxArrayString& cmd);
	int SetInjectedGames(wxArrayString& games, wxArrayString& cmd);

	uMod_Server* Server;

public:
	wxNotebook* Notebook;
private:

	wxButton* OpenButton;
	wxButton* DirectoryButton;
	wxButton* UpdateButton;
	wxButton* ReloadButton;


	wxMenuBar* MenuBar;
	wxMenu* MenuMain;
	wxMenu* MenuHelp;

	wxBoxSizer* MainSizer;
	wxBoxSizer* ButtonSizer;


	int NumberOfGames;
	int MaxNumberOfGames;
	uMod_Client** Clients;

	int LoadTemplate(void);
	int SaveTemplate(void);
	wxArrayString SaveFile_Exe;
	wxArrayString SaveFile_Name;


	void InstallHook(void);
	void RemoveHook(void);

	HMODULE H_DX9_DLL;

	wxString LastError;

	DECLARE_EVENT_TABLE();
};

class MyApp : public wxApp
{
public:
	virtual ~MyApp();
	virtual bool OnInit();

private:
	HANDLE CheckForSingleRun;
};




#endif
