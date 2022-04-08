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

uMod_Client::uMod_Client(PipeStruct& pipe, uMod_Frame* frame) : wxThread(wxTHREAD_JOINABLE)
{
	Pipe.In = pipe.In;
	Pipe.Out = pipe.Out;
	MainFrame = frame;
}

uMod_Client::~uMod_Client(void)
{
	if (Pipe.Out != INVALID_HANDLE_VALUE)
	{
		DisconnectNamedPipe(Pipe.Out);
		CloseHandle(Pipe.Out);
		Pipe.Out = INVALID_HANDLE_VALUE;
	}
	if (Pipe.In != INVALID_HANDLE_VALUE)
	{
		DisconnectNamedPipe(Pipe.In);
		CloseHandle(Pipe.In);
		Pipe.In = INVALID_HANDLE_VALUE;
	}
}

void* uMod_Client::Entry(void)
{
	char buffer[SMALL_BUFSIZE];
	while (1)
	{
		unsigned long size;
		bool ret = ReadFile(
			Pipe.In,        // handle to pipe
			buffer,    // buffer to receive data
			SMALL_BUFSIZE, // size of buffer
			&size, // number of bytes read
			NULL);        // not overlapped I/O

		if (ret || GetLastError() == ERROR_MORE_DATA)
		{
			unsigned int pos = 0;
			MsgStruct* commands;
			bool update_textures = false;
			while (pos < size - sizeof(MsgStruct))
			{
				commands = (MsgStruct*)&buffer[pos];
				//unsigned int add_length = 0;
				/*
				switch (commands->Control)
				{

				}
				*/
				pos += sizeof(MsgStruct);// + add_length;
			}
		}
		else
		{
			break;
		}
	}
	CloseHandle(Pipe.In);
	Pipe.In = INVALID_HANDLE_VALUE;
	CloseHandle(Pipe.Out);
	Pipe.Out = INVALID_HANDLE_VALUE;

	uMod_Event event(uMod_EVENT_TYPE, ID_Delete_Game);
	event.SetClient(this);
	wxPostEvent(MainFrame, event);

	return NULL;
}

