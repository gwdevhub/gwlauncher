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



uMod_GameInfo::uMod_GameInfo(void)
{
  Checked = NULL;
  NumberOfChecked = 0;
  LengthOfChecked = 0;
  Init();
}


uMod_GameInfo::~uMod_GameInfo(void)
{
  if (Checked!=NULL) delete [] Checked;
}

void uMod_GameInfo::Init(void)
{
  SaveSingleTexture = false;
  SaveAllTextures = false;

  KeyBack = -1;
  KeySave = -1;
  KeyNext = -1;
  FontColour[0]=255;FontColour[1]=0;FontColour[2]=0;
  TextureColour[0]=0;TextureColour[1]=255;TextureColour[2]=0;
  NumberOfChecked = 0;
  SavePath.Empty();
  OpenPath.Empty();
  Files.Empty();
}

int uMod_GameInfo::SaveToFile( const wxString &file_name)
{
  wxFile file;

  //if (!file.Access(name, wxFile::write)) return -1;
  file.Open(file_name, wxFile::write);
  if (!file.IsOpened())  {return -1;}

  wxString content;
  if (SavePath.Len()>0)
  {
    content.Printf( L"SavePath:%ls\n", SavePath.wc_str());
    file.Write( content.char_str(), content.Len());
  }

  if (OpenPath.Len()>0)
  {
    content.Printf( L"OpenPath:%ls\n", OpenPath.wc_str());
    file.Write( content.char_str(), content.Len());
  }

  content.Printf( L"SaveAllTextures:%d\nSaveSingleTexture:%d\n", SaveAllTextures, SaveSingleTexture);
  file.Write( content.char_str(), content.Len());

  if (KeyBack>=0)
  {
    content.Printf( L"KeyBack:%d\n", KeyBack);
    file.Write( content.char_str(), content.Len());
  }
  if (KeySave>=0)
  {
    content.Printf( L"KeySave:%d\n", KeySave);
    file.Write( content.char_str(), content.Len());
  }
  if (KeyNext>=0)
  {
    content.Printf( L"KeyNext:%d\n", KeyNext);
    file.Write( content.char_str(), content.Len());
  }

  content.Printf( L"FontColour:%d,%d,%d\n", FontColour[0], FontColour[1], FontColour[2]);
  file.Write( content.char_str(), content.Len());
  content.Printf( L"TextureColour:%d,%d,%d\n", TextureColour[0], TextureColour[1], TextureColour[2]);
  file.Write( content.char_str(), content.Len());

  int num = Files.GetCount();

  for (int i=0; i<num; i++)
  {
    if (i<NumberOfChecked)
    {
      if (Checked[i]) content.Printf( L"Add_true:%ls\n", Files[i].wc_str());
      else content.Printf( L"Add_false:%ls\n", Files[i].wc_str());
    }
    else content.Printf( L"Add_true:%ls\n", Files[i].wc_str());
    file.Write( content.char_str(), content.Len());
  }

  file.Close();
  return 0;
}


int uMod_GameInfo::LoadFromFile( const wxString &file_name)
{
  Init();
  if (file_name.Len()==0) return -1;

  wxFile file;
  if (!file.Access(file_name, wxFile::read)) return -1;
  file.Open(file_name, wxFile::read);
  if (!file.IsOpened())  {return -1;}

  unsigned len = file.Length();

  unsigned char* buffer;
  try {buffer = new unsigned char [len+1];}
  catch (...) {return -1;}

  unsigned int result = file.Read( buffer, len);
  file.Close();

  if (result != len) {delete [] buffer; return -1;}

  buffer[len]=0;

  wxString content;
  content =  buffer;
  delete [] buffer;

  wxStringTokenizer token( content, "\n");

  int num = token.CountTokens();

  if (LengthOfChecked<num)
  {
    if (Checked!=NULL) delete [] Checked;
    try {Checked = new bool [num+100];}
    catch (...) {Checked=NULL;LengthOfChecked=0; return -1;}
    LengthOfChecked = num+100;
  }

  wxString line;
  wxString command;
  wxString temp;
  Files.Alloc(num);
  for (int i=0; i<num; i++)
  {
    line = token.GetNextToken();
    command = line.BeforeFirst(':');


    if (command == L"Add_true")
    {
      Checked[NumberOfChecked++] = true;
      Files.Add(line.AfterFirst(':'));
    }
    else if (command == L"Add_false")
    {
      Checked[NumberOfChecked++] = false;
      Files.Add(line.AfterFirst(':'));
    }
    else if (command == L"SavePath") SavePath = line.AfterFirst(':');
    else if (command == L"OpenPath") OpenPath = line.AfterFirst(':');
    else if (command == L"SaveAllTextures")
    {
      temp = line.AfterFirst(':');
      if (temp[0]=='0') SaveAllTextures = false;
      else SaveAllTextures = true;
    }
    else if (command == L"SaveSingleTexture")
    {
      temp = line.AfterFirst(':');
      if (temp[0]=='0') SaveSingleTexture = false;
      else SaveSingleTexture = true;
    }
    else if  (command == L"KeyBack")
    {
      temp = line.AfterFirst(':');
      long key;
      if (temp.ToLong( &key)) KeyBack = key;
      else KeyBack = -1;
    }
    else if  (command == L"KeySave")
    {
      temp = line.AfterFirst(':');
      long key;
      if (temp.ToLong( &key)) KeySave = key;
      else KeySave = -1;
    }
    else if  (command == L"KeyNext")
    {
      temp = line.AfterFirst(':');
      long key;
      if (temp.ToLong( &key)) KeyNext = key;
      else KeyNext = -1;
    }
    else if  (command == L"FontColour")
    {
      temp = line.AfterFirst(':');
      temp = temp.BeforeFirst(',');
      long colour;
      if (temp.ToLong( &colour)) FontColour[0] = colour;
      else FontColour[0] = 255;
      temp = line.AfterFirst(':');
      temp = temp.AfterFirst(',');
      temp = temp.BeforeFirst(',');
      if (temp.ToLong( &colour)) FontColour[1] = colour;
      else FontColour[1] = 0;
      temp = line.AfterFirst(':');
      temp = temp.AfterLast(',');
      if (temp.ToLong( &colour)) FontColour[2] = colour;
      else FontColour[2] = 0;
    }
    else if  (command == L"TextureColour")
    {
      temp = line.AfterFirst(':');
      temp = temp.BeforeFirst(',');
      long colour;
      if (temp.ToLong( &colour)) TextureColour[0] = colour;
      else TextureColour[0] = 0;
      temp = line.AfterFirst(':');
      temp = temp.AfterFirst(',');
      temp = temp.BeforeFirst(',');
      if (temp.ToLong( &colour)) TextureColour[1] = colour;
      else TextureColour[1] = 255;
      temp = line.AfterFirst(':');
      temp = temp.AfterLast(',');
      if (temp.ToLong( &colour)) TextureColour[2] = colour;
      else TextureColour[2] = 0;
    }

/*
    if (NumberOfChecked>=LengthOfChecked)
    {
      bool *t_bool;
      try {t_bool = new bool [LengthOfChecked+100];}
      catch (...) {return -1;}
      for (int i=0; i<LengthOfChecked; i++) t_bool[i]=Checked[i];
      delete [] Checked;
      Checked = t_bool;
      LengthOfChecked +=100;
    }
    */
  }
  return 0;
}


int uMod_GameInfo::GetChecked( bool* array, int num) const
{
  for (int i=0; i<num && i<NumberOfChecked; i++) array[i] = Checked[i];
  return 0;
}

int uMod_GameInfo::SetChecked( bool* array, int num)
{
  if (num>LengthOfChecked)
  {
    if (Checked!=NULL) delete [] Checked;
    try {Checked = new bool [num+100];}
    catch (...) {Checked=NULL; LengthOfChecked = 0; return -1;}
    LengthOfChecked = num+100;
  }
  for (int i=0; i<num; i++) Checked[i] = array[i];
  NumberOfChecked = num;
  return 0;
}

int uMod_GameInfo::SetSaveSingleTexture(bool val)
{
  SaveSingleTexture = val;
  return 0;
}

int uMod_GameInfo::SetSaveAllTextures(bool val)
{
  SaveAllTextures = val;
  return 0;
}

void uMod_GameInfo::SetFiles(const wxArrayString &files)
{
  Files = files;
}

void uMod_GameInfo::GetFiles( wxArrayString &files) const
{
  files = Files;
}

uMod_GameInfo& uMod_GameInfo::operator = (const  uMod_GameInfo &rhs)
{
  SaveSingleTexture = rhs.SaveSingleTexture;
  SaveAllTextures = rhs.SaveAllTextures;

  KeyBack = rhs.KeyBack;
  KeySave = rhs.KeySave;
  KeyNext = rhs.KeyNext;

  if (LengthOfChecked<rhs.LengthOfChecked)
  {
    if (Checked!=NULL) delete [] Checked;
    Checked = new bool [rhs.LengthOfChecked];
    LengthOfChecked = rhs.LengthOfChecked;
  }
  NumberOfChecked = rhs.NumberOfChecked;
  for (int i=0; i<NumberOfChecked; i++) Checked[i] = rhs.Checked[i];

  SavePath = rhs.SavePath;
  OpenPath = rhs.OpenPath;
  Files = rhs.Files;

  for (int i=0; i<3; i++) FontColour[i]=rhs.FontColour[i];
  for (int i=0; i<3; i++) TextureColour[i]=rhs.TextureColour[i];

  return *this;
}

