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


#ifndef uMod_SETTINGS_H_
#define uMod_SETTINGS_H_


#include "uMod_Main.h"

class uMod_Settings
{
public:
	uMod_Settings(void);
	uMod_Settings(uMod_Settings& set);


	int XSize, YSize;
	int XPos, YPos;
	bool UseHook;
	wxString Language;

	int Load(void);
	int Save(void);

};


#endif /* uMod_SETTINGS_H_ */
