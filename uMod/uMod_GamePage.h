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



#ifndef uMod_GAMEPAGE_H_
#define uMod_GAMEPAGE_H_
#include "uMod_Main.h"

// this page is opened if a game is started.
class uMod_GamePage : public wxScrolledWindow
{
public:
	uMod_GamePage(wxNotebook* parent, const wxString& exe, const wxString& save, PipeStruct& pipe);
	virtual ~uMod_GamePage(void);

	int AddTexture(const wxString& file_name);

	int UpdateGame(void);
	int ReloadGame(void);

	int SaveTemplate(const wxString& file_name);
	int LoadTemplate(const wxString& file_name);

	wxString GetExeName(void) { return ExeName; }
	wxString GetTemplateName(void) { return TemplateName; }

	int SetOpenPath(const wxString& path) { return Game.SetOpenPath(path); }
	wxString GetOpenPath(void) { return Game.GetOpenPath(); }

	int SetSavePath(const wxString& path);
	wxString GetSavePath(void) { return Game.GetSavePath(); }


	void OnButtonUp(wxCommandEvent& WXUNUSED(event));
	void OnButtonDown(wxCommandEvent& WXUNUSED(event));
	void OnButtonDelete(wxCommandEvent& WXUNUSED(event));

	int UpdateLanguage(void);

	wxString LastError;

private:

	int GetSettings(void);
	int SetColour(wxTextCtrl** txt, int* colour);
	int GetColour(wxTextCtrl* txt, int def);

	wxString ExeName;
	wxString TemplateName;

	wxBoxSizer* SizerKeys[2];
	wxTextCtrl* TextKeyBack;
	wxTextCtrl* TextKeySave;
	wxTextCtrl* TextKeyNext;
	wxChoice* ChoiceKeyBack;
	wxChoice* ChoiceKeySave;
	wxChoice* ChoiceKeyNext;

	wxBoxSizer* FontColourSizer;
	wxTextCtrl* FontColour[4];
	wxBoxSizer* TextureColourSizer;
	wxTextCtrl* TextureColour[4];

	wxBoxSizer* MainSizer;

	wxTextCtrl* TemplateFile;
	wxCheckBox* SaveAllTextures;
	wxCheckBox* SaveSingleTexture;
	wxTextCtrl* SavePath;

	wxBoxSizer** CheckBoxHSizers;
	wxButton** CheckButtonUp;
	wxButton** CheckButtonDown;
	wxButton** CheckButtonDelete;

	wxCheckBox** CheckBoxes;
	int NumberOfEntry;
	int MaxNumberOfEntry;


	wxArrayString Files;
	uMod_GameInfo Game;
	uMod_GameInfo GameOld;

	uMod_Sender Sender;


	//DECLARE_EVENT_TABLE();
};

#endif /* uMod_GAMEPAGE_H_ */
