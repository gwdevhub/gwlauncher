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


uMod_Sender::uMod_Sender(PipeStruct& pipe) : Pipe(pipe)
{
	OldTextures = NULL;
	OldTexturesNum = 0;
	try { Buffer = new char[BIG_BUFSIZE]; }
	catch (...) { Buffer = NULL; }
}

uMod_Sender::~uMod_Sender(void)
{
	if (Buffer != NULL) delete[] Buffer;
	if (OldTextures != NULL) delete[] OldTextures;
}


int uMod_Sender::Send(const uMod_GameInfo& game, const uMod_GameInfo& game_old, bool force, wxArrayString* comments)
{
	LastError.Empty();
	int key = game.GetKeyBack();
	if (key >= 0 && key != game_old.GetKeyBack())
	{
		key = Language->KeyValues[key];
		SendKey(key, CONTROL_KEY_BACK);
	}
	key = game.GetKeySave();
	if (key >= 0 && key != game_old.GetKeySave())
	{
		key = Language->KeyValues[key];
		SendKey(key, CONTROL_KEY_SAVE);
	}
	key = game.GetKeyNext();
	if (key >= 0 && key != game_old.GetKeyNext())
	{
		key = Language->KeyValues[key];
		SendKey(key, CONTROL_KEY_NEXT);
	}

	int colour[3], colour_old[3];
	game.GetFontColour(colour);
	game_old.GetFontColour(colour_old);
	for (int i = 0; i < 3; i++) if (colour[i] != colour_old[i])
	{
		SendColour(colour, CONTROL_FONT_COLOUR);
		break;
	}

	game.GetTextureColour(colour);
	game_old.GetTextureColour(colour_old);
	for (int i = 0; i < 3; i++) if (colour[i] != colour_old[i])
	{
		SendColour(colour, CONTROL_TEXTURE_COLOUR);
		break;
	}


	if (game.GetSaveSingleTexture() != game_old.GetSaveSingleTexture()) SendSaveSingleTexture(game.GetSaveSingleTexture());
	if (game.GetSaveAllTextures() != game_old.GetSaveAllTextures()) SendSaveAllTextures(game.GetSaveAllTextures());

	wxString path;
	path = game.GetSavePath();
	if (path != game_old.GetSavePath()) SendPath(path);


	if (game.GetNumberOfFiles() <= 0 && OldTexturesNum == 0 && OldTextures == NULL)
	{
		if (LastError.Len() > 0) return 1;
		else return 0;
	}

	wxArrayString files;

	game.GetFiles(files);
	int num = files.GetCount();
	bool* checked = NULL;
	if (num > 0)
	{
		if (GetMemory(checked, num)) { LastError << Language->Error_Memory; return -1; }
		game.GetChecked(checked, num);
	}

	AddTextureClass* tex = NULL;//new AddTextureClass[num+OldTexturesNum];
	if (GetMemory(tex, num + OldTexturesNum)) { LastError << Language->Error_Memory; return -1; }
	wxString comment;

	if (force || OldTexturesNum == 0 || OldTextures == NULL)
	{
		//reload everything
		for (int i = 0; i < num; i++)
		{
			uMod_File file(files[i]);
			if (file.GetComment(comment))
			{
				LastError << file.LastError;
				file.LastError.Empty();
			}
			tex[i].Comment = comment;

			tex[i].Add = checked[i];
			tex[i].Force = true;
			tex[i].File = files[i];
			if (file.GetContent(tex[i], checked[i]))
			{
				LastError << file.LastError;
				file.LastError.Empty();
			}
		}

		// append all packages, which was added but (maybe) are no longer in the list
		int append = 0;
		if (OldTexturesNum > 0 && OldTextures != NULL)
		{
			for (int i = 0; i < OldTexturesNum; i++)
			{
				if (OldTextures[i].Add)
				{
					bool del = true;
					for (int j = 0; j < num; j++) if (OldTextures[i].File == tex[j].File) { del = false; break; }
					if (del)
					{
						tex[num + append].InheriteMemory(OldTextures[i]);
						tex[num + append].Add = false;
						tex[num + append].Force = true;
						append++;
					}
				}
			}
		}
		SendTextures(num + append, tex);
	}
	else
	{
		//search for same packages to avoid reload from disk

		//first step: maybe the order did not change
		int pos = 0;
		bool* hit = NULL;
		if (GetMemory(hit, OldTexturesNum, false)) { LastError << Language->Error_Memory; return -1; }
		for (pos = 0; pos < num && pos < OldTexturesNum; pos++)
		{
			if (OldTextures[pos].File == files[pos])
			{
				tex[pos].InheriteMemory(OldTextures[pos]);
				tex[pos].Force = false;
				hit[pos] = true;
			}
			else
			{
				break;
			}
		}

		//second step: if the order changed -> looking brute force
		for (int i = pos; i < num; i++) for (int j = pos; j < OldTexturesNum; j++)
		{
			if (!hit[j] && OldTextures[j].File == files[i])
			{
				tex[i].InheriteMemory(OldTextures[j]);
				tex[i].Force = false;
				hit[j] = true;
			}
		}

		//next step, set Add to true or false and load packages, which are not loaded
		for (int i = 0; i < num; i++)
		{
			tex[i].Add = checked[i];

			if (tex[i].Len == 0 || (tex[i].Add && !tex[i].Loaded))
			{
				uMod_File file(files[i]);
				if (file.GetComment(comment))
				{
					LastError << file.LastError;
					file.LastError.Empty();
				}
				tex[i].Comment = comment;

				tex[i].Add = checked[i];
				tex[i].Force = true;
				tex[i].File = files[i];
				if (file.GetContent(tex[i], checked[i]))
				{
					LastError << file.LastError;
					file.LastError.Empty();
				}
			}
		}

		// append all packages, which was added but are no longer in the list
		int append = 0;
		if (OldTexturesNum != 0 && OldTextures != NULL)
		{
			for (int j = pos; j < OldTexturesNum; j++)
			{
				if (!hit[j] && OldTextures[j].Add)
				{
					bool del = true;
					for (int i = pos; i < num; i++) if (OldTextures[j].File == tex[i].File) { del = false; break; }
					if (del)
					{
						tex[num + append].InheriteMemory(OldTextures[j]);
						tex[num + append].Add = false;
						tex[num + append].Force = true;
						append++;
					}
				}
			}
		}
		SendTextures(num + append, tex);
		if (hit != NULL) delete[] hit;
	}
	if (checked != NULL) delete[] checked;

	if (comments != NULL && num > 0)
	{
		comments->Empty();
		comments->Alloc(num);
		for (int i = 0; i < num; i++) comments->Add(tex[i].Comment);
	}

	if (OldTextures != NULL) delete[] OldTextures;

	OldTexturesNum = num;
	OldTextures = tex;

	if (LastError.Len() > 0) return 1;
	else return 0;
}



int uMod_Sender::SendSaveAllTextures(bool val)
{
	MsgStruct msg;
	msg.Control = CONTROL_SAVE_ALL;
	if (val) msg.Value = 1;
	else msg.Value = 0;
	msg.Hash = 0u;

	return SendToGame((void*)&msg, sizeof(MsgStruct));
}

int uMod_Sender::SendSaveSingleTexture(bool val)
{
	MsgStruct msg;
	msg.Control = CONTROL_SAVE_SINGLE;
	if (val) msg.Value = 1;
	else msg.Value = 0;
	msg.Hash = 0u;

	return SendToGame((void*)&msg, sizeof(MsgStruct));
}


int uMod_Sender::SendTextures(unsigned int num, AddTextureClass* tex)
{
	if (Buffer == NULL) return (RETURN_NO_MEMORY);

	MsgStruct* msg;
	int pos = 0;
	for (unsigned int i = 0u; i < num; i++) for (unsigned int j = 0u; j < tex[i].Num; j++)
	{
		if (tex[i].Force || !tex[i].Add || !tex[i].WasAdded[j])
			// if force==true we must update
			// tex[i].Add!=true we can always remove, cause removing does cost time in the render thread
			// if tex[i].Add==true and WasAdded[j]!=true this texture was not loaded but should be loaded, so maybe we can load it now
		{
			bool hit = false; //we send only if this has was not send before
			unsigned long temp_hash = tex[i].Hash[j];
			for (unsigned int ii = 0u; ii < i && !hit; ii++) for (unsigned int jj = 0u; jj < tex[ii].Num && !hit; jj++) if (temp_hash == tex[ii].Hash[jj]) hit = true;
			for (unsigned int jj = 0u; jj < j && !hit; jj++) if (temp_hash == tex[i].Hash[jj]) hit = true;
			if (hit)
			{
				tex[i].WasAdded[j] = false; //no matter what is done for this hash before, this texture is not added!
				continue;
			}

			if (tex[i].Size[j] + 2 * sizeof(MsgStruct) + pos > BIG_BUFSIZE) //the buffer is full
			{
				msg = (MsgStruct*)&Buffer[pos];
				msg->Control = CONTROL_MORE_TEXTURES; // we will send more textures
				pos += sizeof(MsgStruct);
				if (int ret = SendToGame(Buffer, pos)) return ret;
				pos = 0;
			}
			unsigned int size = tex[i].Size[j];
			msg = (MsgStruct*)&Buffer[pos];
			msg->Hash = temp_hash;
			msg->Value = size;
			pos += sizeof(MsgStruct);

			if (tex[i].Add)
			{
				msg->Control = CONTROL_FORCE_RELOAD_TEXTURE_DATA; //we always force because whether force is true or not
				//if (Add==true && WasAdded[j]!=true) the texture is loaded the first time, or in previous loads it could not be loaded
				//because an other texture was send with the same hash, in all cases forcing is the best choice (atm)
				char* temp = tex[i].Textures[j];
				if (temp != NULL)
				{
					for (unsigned int l = 0; l < size; l++) Buffer[pos + l] = temp[l];
					pos += size;
				}
				tex[i].WasAdded[j] = true;
			}
			else
			{
				msg->Control = CONTROL_REMOVE_TEXTURE;
				tex[i].WasAdded[j] = false;
			}
		}
		else if (tex[i].Add && tex[i].WasAdded[j]) // this texture could be removed, due to a rearranging of the list
		{
			bool hit = false; //we send only if this has was not send before
			unsigned long temp_hash = tex[i].Hash[j];
			for (unsigned int ii = 0u; ii < i && !hit; ii++) for (unsigned int jj = 0u; jj < tex[ii].Num && !hit; jj++) if (temp_hash == tex[ii].Hash[jj]) hit = true;
			for (unsigned int jj = 0u; jj < j && !hit; jj++) if (temp_hash == tex[i].Hash[jj]) hit = true;
			if (hit)
			{
				tex[i].WasAdded[j] = false; // due to rearranging this texture is replaced by an other texture
			}
		}
	}
	if (pos) if (int ret = SendToGame(Buffer, pos)) return ret;

	if (LastError.Len() > 0) return 1;
	else return 0;
}


int uMod_Sender::SendKey(int key, int ctr)
{
	MsgStruct msg;
	msg.Control = ctr;
	msg.Value = key;
	msg.Hash = 0u;

	return SendToGame((void*)&msg, sizeof(MsgStruct));
}

#define D3DCOLOR_ARGB(a,r,g,b) ((DWORD)((((a)&0xff)<<24)|(((r)&0xff)<<16)|(((g)&0xff)<<8)|((b)&0xff)))

int uMod_Sender::SendColour(int* colour, int ctr)
{
	MsgStruct msg;
	msg.Control = ctr;
	msg.Value = D3DCOLOR_ARGB(255, colour[0], colour[1], colour[2]);
	msg.Hash = 0u;

	return SendToGame((void*)&msg, sizeof(MsgStruct));
}
#undef D3DCOLOR_ARGB


int uMod_Sender::SendPath(const wxString& path)
{
	MsgStruct* msg = (MsgStruct*)Buffer;

	msg->Hash = 0u;
	msg->Control = CONTROL_SET_DIR;

	const wchar_t* file = path.wc_str();
	wchar_t* buff_file = (wchar_t*)&Buffer[sizeof(MsgStruct)];
	int len = 0;
	while (file[len] && (sizeof(MsgStruct) + len * sizeof(wchar_t)) < BIG_BUFSIZE) { buff_file[len] = file[len]; len++; };
	if ((sizeof(MsgStruct) + len * sizeof(wchar_t)) < BIG_BUFSIZE) buff_file[len] = 0;
	len++;

	msg->Value = len * sizeof(wchar_t);
	return SendToGame(Buffer, sizeof(MsgStruct) + len * sizeof(wchar_t));
}


int uMod_Sender::SendToGame(void* msg, unsigned long len)
{
	if (len == 0) return (RETURN_BAD_ARGUMENT);
	unsigned long num;

	if (Pipe.Out == INVALID_HANDLE_VALUE) { LastError << Language->Error_NoPipe; return -1; }
	bool ret = WriteFile(Pipe.Out, (const void*)msg, len, &num, NULL);
	if (!ret || len != num) { LastError << Language->Error_WritePipe; return -1; }
	if (!FlushFileBuffers(Pipe.Out)) { LastError << Language->Error_FlushPipe; return -1; }
	return 0;
}

