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

uMod_Language* Language = NULL;

uMod_Language::uMod_Language(void)
{
	LoadDefault();
	LoadKeys();
}

uMod_Language::uMod_Language(const wxString& name)
{
	LoadLanguage(name);
	LoadKeys();
	LastError.Empty();
}


int uMod_Language::GetLanguages(wxArrayString& lang)
{
	wxArrayString files;
	wxString dir = wxGetCwd();
	dir << "/languages";
	wxDir::GetAllFiles(dir, &files, "uMod_LanguagePack_*.txt");
	wxDir::GetAllFiles(dir, &files, "uMod_LanguagePackU_*.txt");
	lang.Empty();
	lang.Alloc(files.GetCount() + 1);
	lang.Add("English");

	wxString temp;
	int num = files.GetCount();
	for (int i = 0; i < num; i++)
	{
		temp = files[i];
		temp = temp.AfterLast('\\');
		temp = temp.AfterFirst('_');
		temp = temp.AfterFirst('_');
		temp = temp.BeforeLast('.');
		lang.Add(temp);
	}
	return 0;
}


int uMod_Language::GetHelpMessage(wxString& help)
{
	wxString file;
	file << "README_" << CurrentLanguage << ".txt";

	wxFile dat;
	bool utf16 = false;
	if (!dat.Access(file, wxFile::read))
	{
		file.Empty();
		file << "READMEU_" << CurrentLanguage << ".txt";
		utf16 = true;
		if (!dat.Access(file, wxFile::read))
		{
			utf16 = false;
			file = "README_English.txt";
			if (!dat.Access(file, wxFile::read)) { LastError << Error_FileOpen << "\n" << file; return -1; }
		}
	}
	dat.Open(file, wxFile::read);
	if (!dat.IsOpened()) { LastError << Error_FileOpen << "\n" << file; return -1; }
	unsigned len = dat.Length();

	unsigned char* buffer;
	try { buffer = new unsigned char[len + 2]; }
	catch (...) { LastError << Error_Memory; return -1; }

	unsigned int result = dat.Read(buffer, len);
	dat.Close();

	if (result != len) { delete[] buffer; LastError << Error_FileRead << "\n" << file; return -1; }

	if (utf16)
	{
		wchar_t* buf = (wchar_t*)buffer;
		len /= 2;
		buf[len] = 0;
		help = buf;
	}
	else
	{
		buffer[len] = 0;
		help = buffer;
	}
	return 0;
}



#define CheckEntry( command, msg, target) \
if ( command == #target ) \
{ \
  target = msg; \
} else

int uMod_Language::LoadLanguage(const wxString& name)
{
	LoadDefault();
	if (name == "English") return 0;

	wxString file_name;
	file_name << "languages/uMod_LanguagePack_" << name << ".txt";

	bool utf16 = false;
	wxFile dat;
	if (!dat.Access(file_name, wxFile::read))
	{
		utf16 = true;
		file_name.Empty();
		file_name << "languages/uMod_LanguagePackU_" << name << ".txt";
		if (!dat.Access(file_name, wxFile::read)) { LastError << Error_FileOpen << "\n" << file_name; return -1; }
	}
	dat.Open(file_name, wxFile::read);
	if (!dat.IsOpened()) { LastError << Error_FileOpen << "\n" << file_name; return -1; }
	unsigned len = dat.Length();

	unsigned char* buffer;
	try { buffer = new unsigned char[len + 2]; }
	catch (...) { LastError << Error_Memory; return -1; }

	unsigned int result = dat.Read(buffer, len);
	dat.Close();

	if (result != len) { delete[] buffer; LastError << Error_FileRead << "\n" << file_name; return -1; }

	CurrentLanguage = name;
	wxString content;

	if (utf16)
	{
		wchar_t* buf = (wchar_t*)buffer;
		len /= 2;
		buf[len] = 0;
		if (buf[0] == 0XFEFF || buf[0] == 0XFFFE) content = &buf[1]; //get rid of the BOM
		else content = buf;
	}
	else
	{
		buffer[len] = 0;
		content = buffer;
	}

	wxStringTokenizer token(content, "|");
	int num = token.CountTokens();
	wxString entry;
	wxString command;
	wxString msg;

	for (int i = 0; i < num; i++)
	{
		entry = token.GetNextToken();
		if (entry[0] == '#') continue;
		command = entry.BeforeFirst(':');
		command.Replace("\r", "");
		command.Replace("\n", "");
		msg = entry.AfterFirst(':');

		while (msg[0] == '\r' || msg[0] == '\n') msg.Remove(0, 1);
		while (msg.Last() == '\n' || msg.Last() == '\r') msg.RemoveLast(1);

		CheckEntry(command, msg, MenuLanguage)
			CheckEntry(command, msg, MenuHelp)
			CheckEntry(command, msg, MenuAbout)
			CheckEntry(command, msg, MenuAcknowledgement)
			CheckEntry(command, msg, MenuStartGame)
			CheckEntry(command, msg, MenuStartGameCMD)
			CheckEntry(command, msg, MenuUseHook)
			CheckEntry(command, msg, MenuAddGame)
			CheckEntry(command, msg, MenuDeleteGame)
			CheckEntry(command, msg, MenuLoadTemplate)
			CheckEntry(command, msg, MenuSaveTemplate)
			CheckEntry(command, msg, MenuSaveTemplateAs)
			CheckEntry(command, msg, MenuSetDefaultTemplate)
			CheckEntry(command, msg, MenuExit)
			CheckEntry(command, msg, MainMenuMain)
			CheckEntry(command, msg, MainMenuHelp)
			CheckEntry(command, msg, ButtonOpen)
			CheckEntry(command, msg, ButtonDirectory)
			CheckEntry(command, msg, ButtonUpdate)
			CheckEntry(command, msg, ButtonReload)
			CheckEntry(command, msg, ChooseFile)
			CheckEntry(command, msg, ChooseDir)
			CheckEntry(command, msg, TextCtrlTemplate)
			CheckEntry(command, msg, CheckBoxSaveSingleTexture)
			CheckEntry(command, msg, CheckBoxSaveAllTextures)
			CheckEntry(command, msg, TextCtrlSavePath)
			CheckEntry(command, msg, SelectLanguage)
			CheckEntry(command, msg, StartGame)
			CheckEntry(command, msg, CommandLine)
			CheckEntry(command, msg, ChooseGame)
			CheckEntry(command, msg, DeleteGame)
			CheckEntry(command, msg, GameAlreadyAdded)
			CheckEntry(command, msg, ExitGameAnyway)
			CheckEntry(command, msg, NoComment)
			CheckEntry(command, msg, Author)
			CheckEntry(command, msg, Error_GameIsHooked)
			CheckEntry(command, msg, Error_ProcessNotStarted)
			CheckEntry(command, msg, Error_RemoveHook)
			CheckEntry(command, msg, Error_FileNotSupported)
			CheckEntry(command, msg, Error_FktNotFound)
			CheckEntry(command, msg, Error_D3DX9NotFound)
			CheckEntry(command, msg, Error_DLLNotFound)
			CheckEntry(command, msg, Error_AlreadyRunning)
			CheckEntry(command, msg, Error_Send)
			CheckEntry(command, msg, Error_KeyTwice)
			CheckEntry(command, msg, Error_NoSavePath)
			CheckEntry(command, msg, Error_KeyNotSet)
			CheckEntry(command, msg, Error_SaveFile)
			CheckEntry(command, msg, Error_NoPipe)
			CheckEntry(command, msg, Error_WritePipe)
			CheckEntry(command, msg, Error_FlushPipe)
			CheckEntry(command, msg, Error_Hash)
			CheckEntry(command, msg, Error_FileOpen)
			CheckEntry(command, msg, Error_FileRead)
			CheckEntry(command, msg, Error_Memory)
			CheckEntry(command, msg, Error_Unzip)
			CheckEntry(command, msg, Error_ZipEntry)
			CheckEntry(command, msg, KeyBack)
			CheckEntry(command, msg, KeySave)
			CheckEntry(command, msg, KeyNext)
			CheckEntry(command, msg, FontColour)
			CheckEntry(command, msg, TextureColour)
		{}
	}

	delete[] buffer;
	return 0;
}
#undef CheckEntry


int uMod_Language::LoadDefault(void)
{
	CurrentLanguage = "English";

	MenuLanguage = "Change language";
	MenuHelp = "Help";
	MenuAbout = "About";
	MenuAcknowledgement = "Acknowledgement";

	MenuStartGame = "Start game through uMod";
	MenuStartGameCMD = "Start game through uMod (with command line)";

	MenuUseHook = "Use global hook";
	MenuAddGame = "Add game";
	MenuDeleteGame = "Delete game";
	MenuLoadTemplate = "Load template";
	MenuSaveTemplate = "Save template";
	MenuSaveTemplateAs = "Save template as ...";
	MenuSetDefaultTemplate = "Set template as default";
	MenuExit = "Exit";

	MainMenuMain = "Main";
	MainMenuHelp = "Help";

	ButtonOpen = "Open texture/package";
	ButtonDirectory = "Set save directory";
	ButtonUpdate = "Update";
	ButtonReload = "Update (reload)";

	ChooseFile = "Choose a file";
	ChooseDir = "Choose a directory";

	TextCtrlTemplate = "Template: ";
	CheckBoxSaveSingleTexture = "Save single texture";
	CheckBoxSaveAllTextures = "Save all textures";
	TextCtrlSavePath = "Save path:";

	SelectLanguage = "Select a language.";

	StartGame = "Select the game to start.";
	CommandLine = "Set command line arguments.";

	ChooseGame = "Select a game binary.";
	DeleteGame = "Select the games to be deleted.";
	GameAlreadyAdded = "Game has been already added.";
	ExitGameAnyway = "Closing OpenTexMod while a game is running might lead to a crash of the game.\nExit anyway?";
	NoComment = "No comment.";
	Author = "Author: ";

	Error_GameIsHooked = "The global hook is active and this game will be injected! Please delete the game from the list or disable the hook.";
	Error_ProcessNotStarted = "The game could not be started.";
	Error_RemoveHook = "Removing the Hook while a game is running might lead to crash.";

	Error_FileNotSupported = "This file type is not supported:\n";
	Error_D3DX9NotFound = "The D3DX9_43.dll (32bit) is not available on your system.\nPlease install the newest DirectX End-User Runtime Installer.";
	Error_DLLNotFound = "Could not load the dll.\nThe dll injection won't work.\nThis might happen if D3DX9_43.dll (32bit) is not installed on your system.\nPlease install the newest DirectX End-User Runtime Web Installer.";
	Error_FktNotFound = "Could not load function out of dll.\nThe dll injection won't work.";
	Error_AlreadyRunning = "An other instance of OpenTexMod is already running.";

	Error_Send = "Could not send to game.";
	Error_KeyTwice = "You have assigned the same key twice.";
	Error_NoSavePath = "You did not set a save path.";
	Error_KeyNotSet = "At least one key is not set.";
	Error_SaveFile = "Could not save to file.";
	Error_NoPipe = "Pipe is not opened.";
	Error_WritePipe = "Could not write in pipe.";
	Error_FlushPipe = "Could not flush pipe buffer.";
	Error_Hash = "Could not find hash, maybe file is not named as *_HASH.dds";
	Error_FileOpen = "Could not open file:";
	Error_FileRead = "Could not read file:";
	Error_Memory = "Could not allocate enough memory.";
	Error_Unzip = "Could not unzip.";
	Error_ZipEntry = "Could not find zip entry.";


	KeyBack = "Back";
	KeySave = "Save";
	KeyNext = "Next";


	FontColour = "Font colour (RGB):";
	TextureColour = "Texture colour (RGB):";
	return 0;
}

#define AddKey( name, key ) \
{ \
  KeyStrings.Add( name ); \
  KeyValues.Add( key ); \
}
int uMod_Language::LoadKeys(void)
{
	KeyStrings.Empty();
	KeyValues.Empty();
	/*

	#define VK_LBUTTON        0x01
	#define VK_RBUTTON        0x02
	#define VK_CANCEL         0x03
	#define VK_MBUTTON        0x04    // NOT contiguous with L & RBUTTON

	#if(_WIN32_WINNT >= 0x0500)
	#define VK_XBUTTON1       0x05    // NOT contiguous with L & RBUTTON
	#define VK_XBUTTON2       0x06    // NOT contiguous with L & RBUTTON
	#endif // _WIN32_WINNT >= 0x0500


	// * 0x07 : unassigned

	 */
	AddKey("VK_BACK", VK_BACK);
	AddKey("VK_TAB", VK_TAB);
	AddKey("VK_CLEAR", VK_CLEAR);
	AddKey("VK_RETURN", VK_RETURN);
	AddKey("VK_SHIFT", VK_SHIFT);
	AddKey("VK_CONTROL", VK_CONTROL);
	AddKey("VK_MENU", VK_MENU);
	AddKey("VK_PAUSE", VK_PAUSE);
	AddKey("VK_CAPITAL", VK_CAPITAL);
	//AddKey(  );
	//AddKey(  );
	//AddKey(  );
	//AddKey(  );
	/*
  #define VK_BACK           0x08
  #define VK_TAB            0x09


  // 0x0A - 0x0B : reserved


  #define VK_CLEAR          0x0C
  #define VK_RETURN         0x0D

  #define VK_SHIFT          0x10
  #define VK_CONTROL        0x11
  #define VK_MENU           0x12
  #define VK_PAUSE          0x13
  #define VK_CAPITAL        0x14

  #define VK_KANA           0x15
  #define VK_HANGEUL        0x15  // old name - should be here for compatibility
  #define VK_HANGUL         0x15
  #define VK_JUNJA          0x17
  #define VK_FINAL          0x18
  #define VK_HANJA          0x19
  #define VK_KANJI          0x19

  */

	AddKey("VK_ESCAPE", VK_ESCAPE);
	/*
	#define VK_ESCAPE         0x1B

	#define VK_CONVERT        0x1C
	#define VK_NONCONVERT     0x1D
	#define VK_ACCEPT         0x1E
	#define VK_MODECHANGE     0x1F
	*/

	AddKey("VK_SPACE", VK_SPACE);
	AddKey("VK_PRIOR", VK_PRIOR);
	AddKey("VK_NEXT", VK_NEXT);
	AddKey("VK_END", VK_END);
	AddKey("VK_HOME", VK_HOME);
	AddKey("VK_LEFT", VK_LEFT);
	AddKey("VK_UP", VK_UP);
	AddKey("VK_RIGHT", VK_RIGHT);
	AddKey("VK_DOWN", VK_DOWN);
	AddKey("VK_SELECT", VK_SELECT);
	AddKey("VK_PRINT", VK_PRINT);
	AddKey("VK_EXECUTE", VK_EXECUTE);
	AddKey("VK_SNAPSHOT", VK_SNAPSHOT);
	AddKey("VK_INSERT", VK_INSERT);
	AddKey("VK_DELETE", VK_DELETE);
	AddKey("VK_HELP", VK_HELP);
	/*
	#define VK_SPACE          0x20
	#define VK_PRIOR          0x21
	#define VK_NEXT           0x22
	#define VK_END            0x23
	#define VK_HOME           0x24
	#define VK_LEFT           0x25
	#define VK_UP             0x26
	#define VK_RIGHT          0x27
	#define VK_DOWN           0x28
	#define VK_SELECT         0x29
	#define VK_PRINT          0x2A
	#define VK_EXECUTE        0x2B
	#define VK_SNAPSHOT       0x2C
	#define VK_INSERT         0x2D
	#define VK_DELETE         0x2E
	#define VK_HELP           0x2F


	// * VK_0 - VK_9 are the same as ASCII '0' - '9' (0x30 - 0x39)
	// * 0x40 : unassigned
	// * VK_A - VK_Z are the same as ASCII 'A' - 'Z' (0x41 - 0x5A)
	*/
	int count = 0x30;
	AddKey("0", count++);
	AddKey("1", count++);
	AddKey("2", count++);
	AddKey("3", count++);
	AddKey("4", count++);
	AddKey("5", count++);
	AddKey("6", count++);
	AddKey("7", count++);
	AddKey("8", count++);
	AddKey("9", count++);

	count = 0x41;
	AddKey("a", count++);
	AddKey("b", count++);
	AddKey("c", count++);
	AddKey("d", count++);
	AddKey("e", count++);
	AddKey("f", count++);
	AddKey("g", count++);
	AddKey("h", count++);
	AddKey("i", count++);
	AddKey("j", count++);
	AddKey("k", count++);
	AddKey("l", count++);
	AddKey("m", count++);
	AddKey("n", count++);
	AddKey("o", count++);
	AddKey("p", count++);
	AddKey("q", count++);
	AddKey("r", count++);
	AddKey("s", count++);
	AddKey("t", count++);
	AddKey("u", count++);
	AddKey("v", count++);
	AddKey("w", count++);
	AddKey("x", count++);
	AddKey("y", count++);
	AddKey("z", count++);

	/*

	#define VK_LWIN           0x5B
	#define VK_RWIN           0x5C
	#define VK_APPS           0x5D


	// 0x5E : reserved
	*/


	AddKey("VK_SLEEP", VK_SLEEP);
	AddKey("VK_NUMPAD0", VK_NUMPAD0);
	AddKey("VK_NUMPAD1", VK_NUMPAD1);
	AddKey("VK_NUMPAD2", VK_NUMPAD2);
	AddKey("VK_NUMPAD3", VK_NUMPAD3);
	AddKey("VK_NUMPAD4", VK_NUMPAD4);
	AddKey("VK_NUMPAD5", VK_NUMPAD5);
	AddKey("VK_NUMPAD6", VK_NUMPAD6);
	AddKey("VK_NUMPAD7", VK_NUMPAD7);
	AddKey("VK_NUMPAD8", VK_NUMPAD8);
	AddKey("VK_NUMPAD9", VK_NUMPAD9);
	AddKey("VK_MULTIPLY", VK_MULTIPLY);
	AddKey("VK_ADD", VK_ADD);
	AddKey("VK_SEPARATOR", VK_SEPARATOR);
	AddKey("VK_SUBTRACT", VK_SUBTRACT);
	AddKey("VK_DECIMAL", VK_DECIMAL);
	AddKey("VK_DIVIDE", VK_DIVIDE);
	AddKey("VK_F1", VK_F1);
	AddKey("VK_F2", VK_F2);
	AddKey("VK_F3", VK_F3);
	AddKey("VK_F4", VK_F4);
	AddKey("VK_F5", VK_F5);
	AddKey("VK_F6", VK_F6);
	AddKey("VK_F7", VK_F7);
	AddKey("VK_F8", VK_F8);
	AddKey("VK_F9", VK_F9);
	AddKey("VK_F10", VK_F10);
	AddKey("VK_F12", VK_F12);
	AddKey("VK_F12", VK_F12);

	/*

  #define VK_SLEEP          0x5F

  #define VK_NUMPAD0        0x60
  #define VK_NUMPAD1        0x61
  #define VK_NUMPAD2        0x62
  #define VK_NUMPAD3        0x63
  #define VK_NUMPAD4        0x64
  #define VK_NUMPAD5        0x65
  #define VK_NUMPAD6        0x66
  #define VK_NUMPAD7        0x67
  #define VK_NUMPAD8        0x68
  #define VK_NUMPAD9        0x69
  #define VK_MULTIPLY       0x6A
  #define VK_ADD            0x6B
  #define VK_SEPARATOR      0x6C
  #define VK_SUBTRACT       0x6D
  #define VK_DECIMAL        0x6E
  #define VK_DIVIDE         0x6F
  #define VK_F1             0x70
  #define VK_F2             0x71
  #define VK_F3             0x72
  #define VK_F4             0x73
  #define VK_F5             0x74
  #define VK_F6             0x75
  #define VK_F7             0x76
  #define VK_F8             0x77
  #define VK_F9             0x78
  #define VK_F10            0x79
  #define VK_F11            0x7A
  #define VK_F12            0x7B
  #define VK_F13            0x7C
  #define VK_F14            0x7D
  #define VK_F15            0x7E
  #define VK_F16            0x7F
  #define VK_F17            0x80
  #define VK_F18            0x81
  #define VK_F19            0x82
  #define VK_F20            0x83
  #define VK_F21            0x84
  #define VK_F22            0x85
  #define VK_F23            0x86
  #define VK_F24            0x87


  // 0x88 - 0x8F : unassigned
  */

	AddKey("VK_NUMLOCK", VK_NUMLOCK);
	AddKey("VK_SCROLL", VK_SCROLL);
	/*

	#define VK_NUMLOCK        0x90
	#define VK_SCROLL         0x91


	// NEC PC-9800 kbd definitions

	#define VK_OEM_NEC_EQUAL  0x92   // '=' key on numpad


	// Fujitsu/OASYS kbd definitions

	#define VK_OEM_FJ_JISHO   0x92   // 'Dictionary' key
	#define VK_OEM_FJ_MASSHOU 0x93   // 'Unregister word' key
	#define VK_OEM_FJ_TOUROKU 0x94   // 'Register word' key
	#define VK_OEM_FJ_LOYA    0x95   // 'Left OYAYUBI' key
	#define VK_OEM_FJ_ROYA    0x96   // 'Right OYAYUBI' key

	//
	// 0x97 - 0x9F : unassigned
	//

	//
	// VK_L* & VK_R* - left and right Alt, Ctrl and Shift virtual keys.
	// Used only as parameters to GetAsyncKeyState() and GetKeyState().
	// No other API or message will distinguish left and right keys in this way.

	#define VK_LSHIFT         0xA0
	#define VK_RSHIFT         0xA1
	#define VK_LCONTROL       0xA2
	#define VK_RCONTROL       0xA3
	#define VK_LMENU          0xA4
	#define VK_RMENU          0xA5

	#if(_WIN32_WINNT >= 0x0500)
	#define VK_BROWSER_BACK        0xA6
	#define VK_BROWSER_FORWARD     0xA7
	#define VK_BROWSER_REFRESH     0xA8
	#define VK_BROWSER_STOP        0xA9
	#define VK_BROWSER_SEARCH      0xAA
	#define VK_BROWSER_FAVORITES   0xAB
	#define VK_BROWSER_HOME        0xAC

	#define VK_VOLUME_MUTE         0xAD
	#define VK_VOLUME_DOWN         0xAE
	#define VK_VOLUME_UP           0xAF
	#define VK_MEDIA_NEXT_TRACK    0xB0
	#define VK_MEDIA_PREV_TRACK    0xB1
	#define VK_MEDIA_STOP          0xB2
	#define VK_MEDIA_PLAY_PAUSE    0xB3
	#define VK_LAUNCH_MAIL         0xB4
	#define VK_LAUNCH_MEDIA_SELECT 0xB5
	#define VK_LAUNCH_APP1         0xB6
	#define VK_LAUNCH_APP2         0xB7

	#endif // _WIN32_WINNT >= 0x0500


	// 0xB8 - 0xB9 : reserved


	#define VK_OEM_1          0xBA   // ';:' for US
	#define VK_OEM_PLUS       0xBB   // '+' any country
	#define VK_OEM_COMMA      0xBC   // ',' any country
	#define VK_OEM_MINUS      0xBD   // '-' any country
	#define VK_OEM_PERIOD     0xBE   // '.' any country
	#define VK_OEM_2          0xBF   // '/?' for US
	#define VK_OEM_3          0xC0   // '`~' for US


	// 0xC1 - 0xD7 : reserved


	// 0xD8 - 0xDA : unassigned


	#define VK_OEM_4          0xDB  //  '[{' for US
	#define VK_OEM_5          0xDC  //  '\|' for US
	#define VK_OEM_6          0xDD  //  ']}' for US
	#define VK_OEM_7          0xDE  //  ''"' for US
	#define VK_OEM_8          0xDF


	// 0xE0 : reserved


	/
	// Various extended or enhanced keyboards

	#define VK_OEM_AX         0xE1  //  'AX' key on Japanese AX kbd
	#define VK_OEM_102        0xE2  //  "<>" or "\|" on RT 102-key kbd.
	#define VK_ICO_HELP       0xE3  //  Help key on ICO
	#define VK_ICO_00         0xE4  //  00 key on ICO

	#if(WINVER >= 0x0400)
	#define VK_PROCESSKEY     0xE5
	#endif // WINVER >= 0x0400

	#define VK_ICO_CLEAR      0xE6


	#if(_WIN32_WINNT >= 0x0500)
	#define VK_PACKET         0xE7
	#endif // _WIN32_WINNT >= 0x0500


	// 0xE8 : unassigned


	//Nokia/Ericsson definitions

	#define VK_OEM_RESET      0xE9
	#define VK_OEM_JUMP       0xEA
	#define VK_OEM_PA1        0xEB
	#define VK_OEM_PA2        0xEC
	#define VK_OEM_PA3        0xED
	#define VK_OEM_WSCTRL     0xEE
	#define VK_OEM_CUSEL      0xEF
	#define VK_OEM_ATTN       0xF0
	#define VK_OEM_FINISH     0xF1
	#define VK_OEM_COPY       0xF2
	#define VK_OEM_AUTO       0xF3
	#define VK_OEM_ENLW       0xF4
	#define VK_OEM_BACKTAB    0xF5

	#define VK_ATTN           0xF6
	#define VK_CRSEL          0xF7
	#define VK_EXSEL          0xF8
	#define VK_EREOF          0xF9
	#define VK_PLAY           0xFA
	#define VK_ZOOM           0xFB
	#define VK_NONAME         0xFC
	#define VK_PA1            0xFD
	#define VK_OEM_CLEAR      0xFE


	 */
	return 0;
}

