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



AddTextureClass::AddTextureClass(void)
{
	Num = 0;
	Textures = NULL;
	Size = NULL;
	Hash = NULL;
	WasAdded = NULL;
	Len = 0;

	Add = false;
	Force = false;
	Loaded = false;
	OwnMemory = false;
}

AddTextureClass::~AddTextureClass(void)
{
	ReleaseMemory();
}

int AddTextureClass::ReleaseMemory(void)
{
	if (OwnMemory)
	{
		if (Size != NULL) delete[] Size;
		if (Hash != NULL) delete[] Hash;
		if (WasAdded != NULL) delete[] WasAdded;


		if (Textures != NULL)
		{
			for (unsigned int i = 0; i < Num && i < Len; i++) if (Textures[i] != NULL) delete[] Textures[i];
			delete[] Textures;
		}
	}
	return 0;
}

int AddTextureClass::SetSize(int num)
{
	Num = 0;
	if (GetMemory(Size, num, 0u)) return -1;
	if (GetMemory(Hash, num)) return -1;
	if (GetMemory(WasAdded, num, false)) return -1;
	if (GetMemory(Textures, num, (char*)0)) return -1;

	OwnMemory = true;
	Len = num;
	return 0;
}

int AddTextureClass::InheriteMemory(AddTextureClass& tex)
{
	if (!tex.OwnMemory)
	{
		if (SetSize(tex.Len)) return -1;
		for (unsigned int i = 0u; i < tex.Len; i++) Hash[i] = tex.Hash[i];
		for (unsigned int i = 0u; i < tex.Len; i++) WasAdded[i] = tex.WasAdded[i];
		for (unsigned int i = 0u; i < tex.Len; i++) Size[i] = tex.Size[i];
		for (unsigned int i = 0u; i < tex.Num; i++) if (tex.Textures[i] != NULL && tex.Size[i] > 0)
		{
			if (GetMemory(Textures[i], tex.Size[i])) return -1;
			for (unsigned int j = 0u; j < tex.Size[i]; j++) Textures[i][j] = tex.Textures[i][j];
			Size[i] = tex.Size[i];
		}
		Len = tex.Len;
		Num = tex.Num;
	}
	else
	{
		ReleaseMemory();
		Hash = tex.Hash;
		WasAdded = tex.WasAdded;
		Size = tex.Size;
		Textures = tex.Textures;
		Len = tex.Len;
		Num = tex.Num;
		OwnMemory = true;
		tex.OwnMemory = false;
	}
	Add = tex.Add;
	Force = tex.Force;
	Loaded = tex.Loaded;

	File = tex.File;
	Comment = tex.Comment;
	return 0;
}
