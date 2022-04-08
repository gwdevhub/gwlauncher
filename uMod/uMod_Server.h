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



#ifndef uMod_SERVER_H_
#define uMod_SERVER_H_
#include "uMod_Main.h"

// an object of this class should be created only once
// it waits for incomming connections (a starting game)
// and if so, it send a message to the mainthread

// Note the server thread can only be killed, if one connect to it and send "uMod_ABORT" as game name
class uMod_Server : public wxThread
{
public:
	uMod_Server(uMod_Frame* frame);
	virtual ~uMod_Server(void);


	void* Entry(void);

private:

	uMod_Frame* MainFrame;
};


#endif /* uMod_SERVER_H_ */
