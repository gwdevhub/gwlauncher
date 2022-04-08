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



#ifndef uMod_LANGUAGE_H_
#define uMod_LANGUAGE_H_

class uMod_Language
{
public:
	uMod_Language(void);
	uMod_Language(const wxString& name);


	int LoadLanguage(const wxString& name);
	int GetLanguages(wxArrayString& lang);
	int GetHelpMessage(wxString& help);
	wxString GetCurrentLanguage(void) { return CurrentLanguage; }

	wxString MenuLanguage;
	wxString MenuHelp;
	wxString MenuAbout;
	wxString MenuAcknowledgement;
	wxString MenuStartGame;
	wxString MenuStartGameCMD;
	wxString MenuUseHook;
	wxString MenuAddGame;
	wxString MenuDeleteGame;
	wxString MenuLoadTemplate;
	wxString MenuSaveTemplate;
	wxString MenuSaveTemplateAs;
	wxString MenuSetDefaultTemplate;
	wxString MenuExit;

	wxString MainMenuMain;
	wxString MainMenuHelp;

	wxString ButtonOpen;
	wxString ButtonDirectory;
	wxString ButtonUpdate;
	wxString ButtonReload;

	wxString ChooseFile;
	wxString ChooseDir;

	wxString TextCtrlTemplate;
	wxString CheckBoxSaveSingleTexture;
	wxString CheckBoxSaveAllTextures;
	wxString TextCtrlSavePath;

	wxString SelectLanguage;

	wxString StartGame;
	wxString CommandLine;

	wxString ChooseGame;
	wxString DeleteGame;
	wxString GameAlreadyAdded;
	wxString ExitGameAnyway;
	wxString NoComment;
	wxString Author;

	wxString Error_GameIsHooked;
	wxString Error_ProcessNotStarted;
	wxString Error_RemoveHook;

	wxString Error_FileNotSupported;
	wxString Error_FktNotFound;
	wxString Error_D3DX9NotFound;
	wxString Error_DLLNotFound;
	wxString Error_AlreadyRunning;

	wxString Error_Send;
	wxString Error_KeyTwice;
	wxString Error_NoSavePath;
	wxString Error_KeyNotSet;
	wxString Error_SaveFile;
	wxString Error_NoPipe;
	wxString Error_WritePipe;
	wxString Error_FlushPipe;
	wxString Error_Hash;
	wxString Error_FileOpen;
	wxString Error_FileRead;
	wxString Error_Memory;
	wxString Error_Unzip;
	wxString Error_ZipEntry;

	wxString KeyBack;
	wxString KeySave;
	wxString KeyNext;
	wxArrayString KeyStrings;
	wxArrayInt KeyValues;


	wxString FontColour;
	wxString TextureColour;


	wxString LastError;

private:
	int LoadDefault(void);
	int LoadKeys(void);

	wxString CurrentLanguage;
};


extern uMod_Language* Language;


#endif /* uMod_LANGUAGE_H_ */
