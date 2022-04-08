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



#ifndef uMod_GAME_H_
#define uMod_GAME_H_
#include "uMod_Main.h"

//this class is intended as a storing object for each game
// one should ad an assignment operator,  loading and saving default values, ...
class uMod_GameInfo
{
public:
	uMod_GameInfo(void);
	~uMod_GameInfo(void);
	void Init(void);


	int SaveToFile(const wxString& file_name);
	int LoadFromFile(const wxString& file_name);

	int GetChecked(bool* array, int num) const;
	int SetChecked(bool* array, int num);

	int SetSaveSingleTexture(bool val);
	bool GetSaveSingleTexture(void) const { return SaveSingleTexture; }

	int SetSaveAllTextures(bool val);
	bool GetSaveAllTextures(void) const { return SaveAllTextures; }

	void SetFiles(const wxArrayString& files);
	void GetFiles(wxArrayString& files) const;
	//void AddTexture( const wxString &textures);

	int GetNumberOfFiles(void) const { return Files.GetCount(); }

	int SendTextures(void);

	int GetKeyBack() const { return KeyBack; }
	int SetKeyBack(int key) { KeyBack = key; return 0; }

	int GetKeySave() const { return KeySave; }
	int SetKeySave(int key) { KeySave = key; return 0; }

	int GetKeyNext() const { return KeyNext; }
	int SetKeyNext(int key) { KeyNext = key; return 0; }

	int SetFontColour(const int* colour) { FontColour[0] = colour[0]; FontColour[1] = colour[1]; FontColour[2] = colour[2]; return 0; }
	int GetFontColour(int* colour) const { colour[0] = FontColour[0]; colour[1] = FontColour[1]; colour[2] = FontColour[2]; return 0; }

	int SetTextureColour(const int* colour) { TextureColour[0] = colour[0]; TextureColour[1] = colour[1]; TextureColour[2] = colour[2]; return 0; }
	int GetTextureColour(int* colour) const { colour[0] = TextureColour[0]; colour[1] = TextureColour[1]; colour[2] = TextureColour[2]; return 0; }

	int SetOpenPath(const wxString& path) { OpenPath = path; return 0; }
	wxString GetOpenPath(void) const { return OpenPath; }

	int SetSavePath(const wxString& path) { SavePath = path; return 0; }
	wxString GetSavePath(void) const { return SavePath; }

	uMod_GameInfo& operator = (const  uMod_GameInfo& rhs);

private:

	bool* Checked;
	int NumberOfChecked;
	int LengthOfChecked;

	bool SaveSingleTexture;
	bool SaveAllTextures;

	wxArrayString Files;

	int KeyBack;
	int KeySave;
	int KeyNext;

	int FontColour[3];
	int TextureColour[3];

	wxString OpenPath;
	wxString SavePath;
};


#endif /* uMod_SERVER_H_ */
