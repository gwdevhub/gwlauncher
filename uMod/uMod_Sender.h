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



#ifndef uMod_SENDER_H_
#define uMod_SENDER_H_

#include "uMod_Main.h"


// an object of this class is owned by each uMod_GamePage object. It act as sender ^^
class uMod_Sender
{
public:
	uMod_Sender(PipeStruct& pipe);
	~uMod_Sender(void);

	int Send(const uMod_GameInfo& game, const uMod_GameInfo& game_old, bool force = false, wxArrayString* comments = NULL);

	wxString LastError;

private:
	int SendSaveAllTextures(bool val);
	int SendSaveSingleTexture(bool val);

	int SendTextures(unsigned int num, AddTextureClass* tex);

	int SendKey(int key, int ctr);

	int SendPath(const wxString& path);

	int SendColour(int* colour, int ctr);

	char* Buffer;
	int SendToGame(void* msg, unsigned long len);

	int AddFile(AddTextureClass* tex, wxString file, bool add, bool force);
	int AddZip(AddTextureClass* tex, wxString file, bool add, bool force, bool tpf);
	int AddContent(char* buffer, unsigned int len, const char* pw, AddTextureClass* tex, bool add, bool force);

	PipeStruct& Pipe;
	AddTextureClass* OldTextures;
	int OldTexturesNum;
};


#endif /* uMod_SENDER_H_ */
