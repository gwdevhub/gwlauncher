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



#ifndef uMod_CLIENT_H_
#define uMod_CLIENT_H_

#include "uMod_Main.h"

// an object of this class is created for each running game
// it reads out of the incoming pipe (it must run as thread)
// if the pipe is closed (Game is canceled) it send a messeage to the main thread
// it should read the error state from the dll, but this is not yet implemented
class uMod_Client : public wxThread
{
public:
	uMod_Client(PipeStruct& pipe, uMod_Frame* frame);
	virtual ~uMod_Client(void);

	void* Entry(void);


	PipeStruct Pipe;

private:
	uMod_Frame* MainFrame;
};

#endif /* uMod_CLIENT_H_ */
