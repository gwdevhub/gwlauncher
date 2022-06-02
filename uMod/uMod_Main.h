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



#ifndef uMod_MAIN_H_
#define uMod_MAIN_H_

// I use eclipse and somehow i need these define or many of the wx classes are unknown to the CDT parser
#ifdef __CDT_PARSER__
#define HAVE_W32API_H
#define __WXMSW__
#define NOPCH
#define _UNICODE
#define wxUSE_NOTEBOOK 1
#define wxUSE_CHECKBOX 1
#define wxUSE_THREADS 1
#define wxUSE_MSGDLG 1
#define wxUSE_MENUS 1
#define wxUSE_BUTTON 1
#define wxUSE_FILEDLG 1
#define wxUSE_DIRDLG 1
#define wxUSE_CHOICE 1
#define wxUSE_FILE 1
#define wxUSE_TEXTCTRL 1
#define wxUSE_CHOICEDLG 1
#define wxUSE_TOOLTIPS 1
#endif

#define WIN32_LEAN_AND_MEAN

#include <wx\wx.h>
#include <wx\notebook.h>
#include <wx/file.h>
#include <wx/dir.h>
#include <wx/tokenzr.h>
#include <wx/dynlib.h>
//#include <wx/thread.h>
//#include "wx/checkbox.h"
//#include <wx/msgdlg.h>
//#include <wx/menu.h>
//#include <wx/button.h>
//#include  <wx/filedlg.h>
//#include <wx/choice.h>
//#include <wx/textctrl.h>
//#include <wx/choicdlg.h>

//#include <windows.h>

#include "uMod_GlobalDefines.h"
#include "uMod_Error.h"


class uMod_Frame;

#define MAX_TEXTURES 1024
enum
{
	ID_Button_Open = wxID_HIGHEST,
	ID_Button_Path,
	ID_Button_Update,
	ID_Button_Reload,
	//ID_Button_Save,
	//ID_Menu_Pref,
	ID_Menu_Exit,
	ID_Menu_Lang,
	ID_Menu_Help,
	ID_Menu_About,
	ID_Menu_Acknowledgement,
	ID_Menu_StartGame,
	ID_Menu_StartGameCMD,
	ID_Menu_UseHook,
	ID_Menu_AddGame,
	ID_Menu_DeleteGame,
	ID_Menu_LoadTemplate,
	ID_Menu_SaveTemplate,
	ID_Menu_SaveTemplateAs,
	ID_Menu_SetDefaultTemplate,
	ID_Add_Game,
	ID_Delete_Game,
	ID_Button_Texture, //this entry must be the last!!
};

#define ABORT_SERVER L"uMod_Abort_Server"
#define uMod_d3d9_Hook_dll L"uMod_d3d9_HI.dll"
#define uMod_d3d9_DI_dll L"uMod_d3d9_DI.dll"

#include "uMod_AddTexture.h"
#include "uMod_Settings.h"
#include "uMod_Language.h"
#include "uMod_Event.h"
#include "uMod_Client.h"
#include "uMod_GameInfo.h"
#include "uMod_File.h"
#include "uMod_Sender.h"
#include "uMod_Server.h"
#include "uMod_GamePage.h"
#include "uMod_DirectInjection.h"
#include "uMod_GUI.h"


template <class T>
int GetMemory(T*& array, int num)
{
	if (array != (T*)0) delete[] array;
	try { array = new T[num]; }
	catch (...) { array = (T*)0; return -1; }
	return 0;
}

template <class T>
int GetMemory(T*& array, int num, T init)
{
	if (array != (T*)0) delete[] array;
	try { array = new T[num]; }
	catch (...) { array = (T*)0; return -1; }
	for (int i = 0; i < num; i++) array[i] = init;
	return 0;
}

template <class T>
int GetMoreMemory(T*& old_array, int old_num, int new_num)
{
	if (new_num <= old_num) return 0;
	T* new_array;
	try { new_array = new T[new_num]; }
	catch (...) { return -1; }
	if (old_array != (T*)0)
	{
		for (int i = 0; i < old_num; i++) new_array[i] = old_array[i];
		delete[] old_array;
	}
	old_array = new_array;
	return 0;
}

#endif
