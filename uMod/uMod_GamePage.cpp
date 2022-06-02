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

#include "uMod_Main.h"


uMod_GamePage::uMod_GamePage(wxNotebook* parent, const wxString& exe, const wxString& save, PipeStruct& pipe)
	: wxScrolledWindow(parent, wxID_ANY, wxDefaultPosition, wxDefaultSize, wxVSCROLL), Sender(pipe)
{
	ExeName = exe;
	TemplateName = save;

	//SetBackgroundColour( *wxLIGHT_GREY);
	//SetBackgroundColour( wxColour( "LIGHT GREY"));

	CheckBoxHSizers = NULL;
	CheckButtonUp = NULL;
	CheckButtonDown = NULL;
	CheckButtonDelete = NULL;
	CheckBoxes = NULL;

	MainSizer = new wxBoxSizer(wxVERTICAL);


	TemplateFile = new wxTextCtrl(this, wxID_ANY, Language->TextCtrlTemplate, wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
	MainSizer->Add((wxWindow*)TemplateFile, 0, wxEXPAND, 0);
	MainSizer->AddSpacer(10);

	SizerKeys[0] = new wxBoxSizer(wxHORIZONTAL);
	SizerKeys[1] = new wxBoxSizer(wxHORIZONTAL);

	TextKeyBack = new wxTextCtrl(this, wxID_ANY, Language->KeyBack, wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
	SizerKeys[0]->Add((wxWindow*)TextKeyBack, 1, wxEXPAND, 0);
	ChoiceKeyBack = new wxChoice(this, wxID_ANY, wxDefaultPosition, wxDefaultSize, Language->KeyStrings);
	SizerKeys[1]->Add((wxWindow*)ChoiceKeyBack, 1, wxEXPAND, 0);

	TextKeySave = new wxTextCtrl(this, wxID_ANY, Language->KeySave, wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
	SizerKeys[0]->Add((wxWindow*)TextKeySave, 1, wxEXPAND, 0);
	ChoiceKeySave = new wxChoice(this, wxID_ANY, wxDefaultPosition, wxDefaultSize, Language->KeyStrings);
	SizerKeys[1]->Add((wxWindow*)ChoiceKeySave, 1, wxEXPAND, 0);

	TextKeyNext = new wxTextCtrl(this, wxID_ANY, Language->KeyNext, wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
	SizerKeys[0]->Add((wxWindow*)TextKeyNext, 1, wxEXPAND, 0);
	ChoiceKeyNext = new wxChoice(this, wxID_ANY, wxDefaultPosition, wxDefaultSize, Language->KeyStrings);
	SizerKeys[1]->Add((wxWindow*)ChoiceKeyNext, 1, wxEXPAND, 0);

	MainSizer->Add(SizerKeys[0], 0, wxEXPAND, 0);
	MainSizer->Add(SizerKeys[1], 0, wxEXPAND, 0);


	FontColourSizer = new wxBoxSizer(wxHORIZONTAL);
	FontColour[0] = new wxTextCtrl(this, wxID_ANY, Language->FontColour, wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
	FontColour[1] = new wxTextCtrl(this, wxID_ANY, "255", wxDefaultPosition, wxDefaultSize);
	FontColour[2] = new wxTextCtrl(this, wxID_ANY, "0", wxDefaultPosition, wxDefaultSize);
	FontColour[3] = new wxTextCtrl(this, wxID_ANY, "0", wxDefaultPosition, wxDefaultSize);
	for (int i = 0; i < 4; i++) FontColourSizer->Add((wxWindow*)FontColour[i], 1, wxEXPAND, 0);

	TextureColourSizer = new wxBoxSizer(wxHORIZONTAL);
	TextureColour[0] = new wxTextCtrl(this, wxID_ANY, Language->TextureColour, wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
	TextureColour[1] = new wxTextCtrl(this, wxID_ANY, "0", wxDefaultPosition, wxDefaultSize);
	TextureColour[2] = new wxTextCtrl(this, wxID_ANY, "255", wxDefaultPosition, wxDefaultSize);
	TextureColour[3] = new wxTextCtrl(this, wxID_ANY, "0", wxDefaultPosition, wxDefaultSize);
	for (int i = 0; i < 4; i++) TextureColourSizer->Add((wxWindow*)TextureColour[i], 1, wxEXPAND, 0);


	MainSizer->Add(FontColourSizer, 0, wxEXPAND, 0);
	MainSizer->Add(TextureColourSizer, 0, wxEXPAND, 0);

	SaveSingleTexture = new wxCheckBox(this, -1, Language->CheckBoxSaveSingleTexture);
	MainSizer->Add((wxWindow*)SaveSingleTexture, 0, wxEXPAND, 0);

	SaveAllTextures = new wxCheckBox(this, -1, Language->CheckBoxSaveAllTextures);
	MainSizer->Add((wxWindow*)SaveAllTextures, 0, wxEXPAND, 0);

	SavePath = new wxTextCtrl(this, wxID_ANY, Language->TextCtrlSavePath, wxDefaultPosition, wxDefaultSize, wxTE_READONLY);
	MainSizer->Add((wxWindow*)SavePath, 0, wxEXPAND, 0);

	MainSizer->AddSpacer(10);

	NumberOfEntry = 0;
	MaxNumberOfEntry = 1024;
	if (GetMemory(CheckBoxes, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return; }
	if (GetMemory(CheckBoxHSizers, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return; }
	if (GetMemory(CheckButtonUp, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return; }
	if (GetMemory(CheckButtonDown, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return; }
	if (GetMemory(CheckButtonDelete, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return; }
	SavePath->SetValue(Language->TextCtrlSavePath);


	SetSizer(MainSizer);

	SetScrollRate(0, 20);
	MainSizer->FitInside(this);

	if (TemplateName.Len() > 0) LoadTemplate(TemplateName);
}

uMod_GamePage::~uMod_GamePage(void)
{
	for (int i = 0; i < NumberOfEntry; i++)
	{
		Unbind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonUp, this, ID_Button_Texture + 3 * i);
		Unbind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDown, this, ID_Button_Texture + 3 * i + 1);
		Unbind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDelete, this, ID_Button_Texture + 3 * i + 2);
	}

	delete[] CheckBoxHSizers;
	delete[] CheckButtonUp;
	delete[] CheckButtonDown;
	delete[] CheckButtonDelete;
	delete[] CheckBoxes;
}

int uMod_GamePage::SetSavePath(const wxString& path)
{
	wxString save_path = Language->TextCtrlSavePath;
	save_path << path;
	SavePath->SetValue(save_path);
	Game.SetSavePath(path);
	return 0;
}


int uMod_GamePage::AddTexture(const wxString& file_name)
{
	if (NumberOfEntry >= MaxNumberOfEntry)
	{
		if (GetMoreMemory(CheckBoxes, MaxNumberOfEntry, MaxNumberOfEntry + 1024)) { LastError = Language->Error_Memory; return -1; }
		if (GetMoreMemory(CheckBoxHSizers, MaxNumberOfEntry, MaxNumberOfEntry + 1024)) { LastError = Language->Error_Memory; return -1; }
		if (GetMoreMemory(CheckButtonUp, MaxNumberOfEntry, MaxNumberOfEntry + 1024)) { LastError = Language->Error_Memory; return -1; }
		if (GetMoreMemory(CheckButtonDown, MaxNumberOfEntry, MaxNumberOfEntry + 1024)) { LastError = Language->Error_Memory; return -1; }
		if (GetMoreMemory(CheckButtonDelete, MaxNumberOfEntry, MaxNumberOfEntry + 1024)) { LastError = Language->Error_Memory; return -1; }
		MaxNumberOfEntry += 1024;
	}
	uMod_File file(file_name);
	if (!file.FileSupported()) { LastError << Language->Error_FileNotSupported << "\n" << file_name; return -1; }

	wxString tool_tip;
	file.GetComment(tool_tip);

	CheckBoxHSizers[NumberOfEntry] = new wxBoxSizer(wxHORIZONTAL);
	CheckBoxes[NumberOfEntry] = new wxCheckBox(this, -1, file_name);
	CheckBoxes[NumberOfEntry]->SetValue(true);
	CheckBoxes[NumberOfEntry]->SetToolTip(tool_tip);

	wchar_t button_txt[2];
	button_txt[0] = 8657;
	button_txt[1] = 0;
	CheckButtonUp[NumberOfEntry] = new wxButton(this, ID_Button_Texture + 3 * NumberOfEntry, button_txt, wxDefaultPosition, wxSize(24, 24));
	Bind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonUp, this, ID_Button_Texture + 3 * NumberOfEntry);

	button_txt[0] = 8659;
	CheckButtonDown[NumberOfEntry] = new wxButton(this, ID_Button_Texture + 3 * NumberOfEntry + 1, button_txt, wxDefaultPosition, wxSize(24, 24));
	Bind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDown, this, ID_Button_Texture + 3 * NumberOfEntry + 1);

	CheckButtonDelete[NumberOfEntry] = new wxButton(this, ID_Button_Texture + 3 * NumberOfEntry + 2, L"X", wxDefaultPosition, wxSize(24, 24));
	Bind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDelete, this, ID_Button_Texture + 3 * NumberOfEntry + 2);

	CheckBoxHSizers[NumberOfEntry]->Add((wxWindow*)CheckBoxes[NumberOfEntry], 1, wxEXPAND, 0);
	CheckBoxHSizers[NumberOfEntry]->Add((wxWindow*)CheckButtonUp[NumberOfEntry], 0, wxEXPAND, 0);
	CheckBoxHSizers[NumberOfEntry]->Add((wxWindow*)CheckButtonDown[NumberOfEntry], 0, wxEXPAND, 0);
	CheckBoxHSizers[NumberOfEntry]->Add((wxWindow*)CheckButtonDelete[NumberOfEntry], 0, wxEXPAND, 0);

	MainSizer->Add(CheckBoxHSizers[NumberOfEntry], 0, wxEXPAND, 0);
	Files.Add(file_name);
	NumberOfEntry++;
	MainSizer->Layout();
	MainSizer->FitInside(this);

	return UpdateGame();
}

int uMod_GamePage::GetSettings(void)
{
	int key_back = ChoiceKeyBack->GetSelection();
	int key_save = ChoiceKeySave->GetSelection();
	int key_next = ChoiceKeyNext->GetSelection();

	if (key_back == key_save && key_back != wxNOT_FOUND) { LastError << Language->Error_KeyTwice; return 1; }
	if (key_back == key_next && key_back != wxNOT_FOUND) { LastError << Language->Error_KeyTwice; return 1; }
	if (key_save == key_next && key_save != wxNOT_FOUND) { LastError << Language->Error_KeyTwice; return 1; }

	bool save_single = SaveSingleTexture->GetValue();
	bool save_all = SaveAllTextures->GetValue();
	wxString path = Game.GetSavePath();
	if ((save_single || save_all) && path.Len() == 0) { LastError << Language->Error_NoSavePath; return 1; }

	if (save_single && (key_back == wxNOT_FOUND || key_save == wxNOT_FOUND || key_next == wxNOT_FOUND)) { LastError << Language->Error_KeyNotSet; return 1; }

	if (key_back != wxNOT_FOUND) Game.SetKeyBack(key_back);
	if (key_save != wxNOT_FOUND) Game.SetKeySave(key_save);
	if (key_next != wxNOT_FOUND) Game.SetKeyNext(key_next);

	Game.SetSaveSingleTexture(save_single);
	Game.SetSaveAllTextures(save_all);

	int colour[3];
	colour[0] = GetColour(FontColour[1], 255);
	colour[1] = GetColour(FontColour[2], 0);
	colour[2] = GetColour(FontColour[3], 0);
	SetColour(&FontColour[1], colour);
	Game.SetFontColour(colour);

	colour[0] = GetColour(TextureColour[1], 0);
	colour[1] = GetColour(TextureColour[2], 255);
	colour[2] = GetColour(TextureColour[3], 0);
	SetColour(&TextureColour[1], colour);
	Game.SetTextureColour(colour);

	Game.SetFiles(Files);

	bool* checked = NULL;
	if (GetMemory(checked, NumberOfEntry)) { LastError = Language->Error_Memory; return -1; }
	for (int i = 0; i < NumberOfEntry; i++) checked[i] = CheckBoxes[i]->GetValue();
	Game.SetChecked(checked, NumberOfEntry);
	delete[] checked;

	return 0;
}

int uMod_GamePage::UpdateGame(void)
{
	if (int ret = GetSettings()) return ret;

	if (int ret = Sender.Send(Game, GameOld, false))
	{
		LastError = Language->Error_Send;
		LastError << "\n" << Sender.LastError;
		Sender.LastError.Empty();
		return ret;
	}

	GameOld = Game;
	return 0;
}


int uMod_GamePage::ReloadGame(void)
{
	if (int ret = GetSettings()) return ret;

	if (int ret = Sender.Send(Game, GameOld, true))
	{
		LastError = Language->Error_Send;
		LastError << "\n" << Sender.LastError;
		Sender.LastError.Empty();
		return ret;
	}

	GameOld = Game;
	return 0;
}

int uMod_GamePage::SaveTemplate(const wxString& file_name)
{
	if (int ret = GetSettings()) return ret;
	if (int ret = Game.SaveToFile(file_name))
	{
		LastError = Language->Error_SaveFile;
		LastError << "\n" << file_name;
		return ret;
	}
	TemplateName = file_name;
	wxString path;
	path = Language->TextCtrlTemplate;
	path << TemplateName;
	TemplateFile->SetValue(path);

	return 0;
}

int uMod_GamePage::LoadTemplate(const wxString& file_name)
{
	if (Game.LoadFromFile(file_name)) return -1;
	TemplateName = file_name;
	wxArrayString comments;

	if (Sender.Send(Game, GameOld, true, &comments) == 0) GameOld = Game;

	wxString path;
	path = Language->TextCtrlTemplate;
	path << TemplateName;
	TemplateFile->SetValue(path);

	int key = Game.GetKeyBack();
	if (key >= 0) ChoiceKeyBack->SetSelection(key);
	key = Game.GetKeySave();
	if (key >= 0) ChoiceKeySave->SetSelection(key);
	key = Game.GetKeyNext();
	if (key >= 0) ChoiceKeyNext->SetSelection(key);

	int colour[3];
	Game.GetFontColour(colour);
	SetColour(&FontColour[1], colour);
	Game.GetTextureColour(colour);
	SetColour(&TextureColour[1], colour);

	SaveSingleTexture->SetValue(Game.GetSaveSingleTexture());
	SaveAllTextures->SetValue(Game.GetSaveAllTextures());

	path = Language->TextCtrlSavePath;
	path << Game.GetSavePath();
	SavePath->SetValue(path);

	int new_NumberOfEntry = Game.GetNumberOfFiles();

	Game.GetFiles(Files);

	if (new_NumberOfEntry >= MaxNumberOfEntry)
	{
		MaxNumberOfEntry = ((NumberOfEntry / 1024) + 1) * 1024;
		if (GetMoreMemory(CheckBoxes, NumberOfEntry, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return -1; }
		if (GetMoreMemory(CheckBoxHSizers, NumberOfEntry, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return -1; }
		if (GetMoreMemory(CheckButtonUp, NumberOfEntry, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return -1; }
		if (GetMoreMemory(CheckButtonDown, NumberOfEntry, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return -1; }
		if (GetMoreMemory(CheckButtonDelete, NumberOfEntry, MaxNumberOfEntry)) { LastError = Language->Error_Memory; return -1; }
	}

	bool* checked = NULL;
	if (GetMemory(checked, new_NumberOfEntry)) { LastError = Language->Error_Memory; return -1; }
	Game.GetChecked(checked, new_NumberOfEntry);


	for (int i = 0; i < NumberOfEntry && i < new_NumberOfEntry; i++)
	{
		CheckBoxes[i]->SetLabel(Files[i]);
		CheckBoxes[i]->SetValue(checked[i]);
		CheckBoxes[i]->SetToolTip(comments[i]);
	}

	for (int i = new_NumberOfEntry; i < NumberOfEntry; i++)
	{
		Unbind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonUp, this, ID_Button_Texture + 3 * i);
		Unbind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDown, this, ID_Button_Texture + 3 * i + 1);
		Unbind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDelete, this, ID_Button_Texture + 3 * i + 2);


		CheckBoxHSizers[i]->Detach((wxWindow*)CheckBoxes[i]);
		CheckBoxHSizers[i]->Detach((wxWindow*)CheckButtonUp[i]);
		CheckBoxHSizers[i]->Detach((wxWindow*)CheckButtonDown[i]);
		CheckBoxHSizers[i]->Detach((wxWindow*)CheckButtonDelete[i]);

		MainSizer->Detach(CheckBoxHSizers[i]);

		delete CheckBoxes[i];
		delete CheckButtonUp[i];
		delete CheckButtonDown[i];
		delete CheckButtonDelete[i];
		delete CheckBoxHSizers[i];
	}
	for (int i = NumberOfEntry; i < new_NumberOfEntry; i++)
	{
		CheckBoxHSizers[i] = new wxBoxSizer(wxHORIZONTAL);
		CheckBoxes[i] = new wxCheckBox(this, -1, Files[i]);
		CheckBoxes[i]->SetValue(checked[i]);
		CheckBoxes[i]->SetToolTip(comments[i]);

		wchar_t button_txt[2];
		button_txt[0] = 8657;
		button_txt[1] = 0;

		CheckButtonUp[i] = new wxButton(this, ID_Button_Texture + 3 * i, button_txt, wxDefaultPosition, wxSize(24, 24));
		Bind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonUp, this, ID_Button_Texture + 3 * i);

		button_txt[0] = 8659;
		CheckButtonDown[i] = new wxButton(this, ID_Button_Texture + 3 * i + 1, button_txt, wxDefaultPosition, wxSize(24, 24));
		Bind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDown, this, ID_Button_Texture + 3 * i + 1);

		CheckButtonDelete[i] = new wxButton(this, ID_Button_Texture + 3 * i + 2, L"X", wxDefaultPosition, wxSize(24, 24));
		Bind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDelete, this, ID_Button_Texture + 3 * i + 2);

		CheckBoxHSizers[i]->Add((wxWindow*)CheckBoxes[i], 1, wxEXPAND, 0);
		CheckBoxHSizers[i]->Add((wxWindow*)CheckButtonUp[i], 0, wxEXPAND, 0);
		CheckBoxHSizers[i]->Add((wxWindow*)CheckButtonDown[i], 0, wxEXPAND, 0);
		CheckBoxHSizers[i]->Add((wxWindow*)CheckButtonDelete[i], 0, wxEXPAND, 0);

		MainSizer->Add(CheckBoxHSizers[i], 0, wxEXPAND, 0);
	}
	delete[] checked;
	NumberOfEntry = new_NumberOfEntry;

	MainSizer->Layout();
	MainSizer->FitInside(this);
	return 0;
}

int uMod_GamePage::SetColour(wxTextCtrl** txt, int* colour)
{
	wxString temp;
	for (int i = 0; i < 3; i++)
	{
		temp.Empty();
		temp << colour[i];
		txt[i]->SetValue(temp);
	}
	return 0;
}

int uMod_GamePage::GetColour(wxTextCtrl* txt, int def)
{
	wxString temp = txt->GetValue();
	long colour;
	if (temp.ToLong(&colour))
	{
		if (colour < 0) colour = 0;
		else if (colour > 255) colour = 255;
	}
	else colour = def;
	return colour;
}


void uMod_GamePage::OnButtonUp(wxCommandEvent& event)
{
	int id = (event.GetId() - ID_Button_Texture) / 3;
	if (id <= 0 || id >= NumberOfEntry) return;

	wxString cpy_str = Files[id];
	Files[id] = Files[id - 1];
	Files[id - 1] = cpy_str;

	CheckBoxes[id]->SetLabel(Files[id]);
	CheckBoxes[id - 1]->SetLabel(Files[id - 1]);

	bool cpy_checked = CheckBoxes[id]->GetValue();
	CheckBoxes[id]->SetValue(CheckBoxes[id - 1]->GetValue());
	CheckBoxes[id - 1]->SetValue(cpy_checked);

	cpy_str = CheckBoxes[id]->GetToolTip()->GetTip();
	wxString cpy_str2 = CheckBoxes[id - 1]->GetToolTip()->GetTip();
	CheckBoxes[id]->SetToolTip(cpy_str2);
	CheckBoxes[id - 1]->SetToolTip(cpy_str);

}

void uMod_GamePage::OnButtonDown(wxCommandEvent& event)
{
	int id = (event.GetId() - ID_Button_Texture - 1) / 3;
	if (id < 0 || id >= NumberOfEntry - 1) return;

	wxString cpy_str = Files[id];
	Files[id] = Files[id + 1];
	Files[id + 1] = cpy_str;

	CheckBoxes[id]->SetLabel(Files[id]);
	CheckBoxes[id + 1]->SetLabel(Files[id + 1]);

	bool cpy_checked = CheckBoxes[id]->GetValue();
	CheckBoxes[id]->SetValue(CheckBoxes[id + 1]->GetValue());
	CheckBoxes[id + 1]->SetValue(cpy_checked);

	cpy_str = CheckBoxes[id]->GetToolTip()->GetTip();
	wxString cpy_str2 = CheckBoxes[id + 1]->GetToolTip()->GetTip();
	CheckBoxes[id]->SetToolTip(cpy_str2);
	CheckBoxes[id + 1]->SetToolTip(cpy_str);
}

void uMod_GamePage::OnButtonDelete(wxCommandEvent& event)
{
	int id = (event.GetId() - ID_Button_Texture - 2) / 3;
	if (id < 0 || id >= NumberOfEntry) return;

	for (int i = id + 1; i < NumberOfEntry; i++) CheckBoxes[i - 1]->SetLabel(Files[i]);
	for (int i = id + 1; i < NumberOfEntry; i++) CheckBoxes[i - 1]->SetValue(CheckBoxes[i]->GetValue());
	wxString cpy_str;
	for (int i = id + 1; i < NumberOfEntry; i++)
	{
		cpy_str = CheckBoxes[i]->GetToolTip()->GetTip();
		CheckBoxes[i - 1]->SetToolTip(cpy_str);
	}

	Files.RemoveAt(id, 1);
	NumberOfEntry--;


	Unbind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonUp, this, ID_Button_Texture + 3 * NumberOfEntry);
	Unbind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDown, this, ID_Button_Texture + 3 * NumberOfEntry + 1);
	Unbind(wxEVT_COMMAND_BUTTON_CLICKED, &uMod_GamePage::OnButtonDelete, this, ID_Button_Texture + 3 * NumberOfEntry + 2);


	CheckBoxHSizers[NumberOfEntry]->Detach((wxWindow*)CheckBoxes[NumberOfEntry]);
	CheckBoxHSizers[NumberOfEntry]->Detach((wxWindow*)CheckButtonUp[NumberOfEntry]);
	CheckBoxHSizers[NumberOfEntry]->Detach((wxWindow*)CheckButtonDown[NumberOfEntry]);
	CheckBoxHSizers[NumberOfEntry]->Detach((wxWindow*)CheckButtonDelete[NumberOfEntry]);

	MainSizer->Detach(CheckBoxHSizers[NumberOfEntry]);

	delete CheckBoxes[NumberOfEntry];
	delete CheckButtonUp[NumberOfEntry];
	delete CheckButtonDown[NumberOfEntry];
	delete CheckButtonDelete[NumberOfEntry];
	delete CheckBoxHSizers[NumberOfEntry];
}


int uMod_GamePage::UpdateLanguage(void)
{
	TextKeyBack->SetValue(Language->KeyBack);
	TextKeySave->SetValue(Language->KeySave);
	TextKeyNext->SetValue(Language->KeyNext);
	FontColour[0]->SetValue(Language->FontColour);
	TextureColour[0]->SetValue(Language->TextureColour);
	SaveAllTextures->SetLabel(Language->CheckBoxSaveAllTextures);
	SaveSingleTexture->SetLabel(Language->CheckBoxSaveSingleTexture);
	wxString temp = Language->TextCtrlSavePath;
	temp << Game.GetSavePath();
	SavePath->SetValue(temp);
	return 0;
}

