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



#ifndef uMod_EVENTTYPE_H_
#define uMod_EVENTTYPE_H_
#include "uMod_Client.h"
#include "uMod_Main.h"


//we need our own event to pass send some arguments from the server or the client thread to the main thread
BEGIN_DECLARE_EVENT_TYPES()
DECLARE_EVENT_TYPE(uMod_EVENT_TYPE, -1)
END_DECLARE_EVENT_TYPES()

class uMod_Event : public wxCommandEvent
{
public:
	uMod_Event(wxEventType commandType = uMod_EVENT_TYPE, int id = 0)
		: wxCommandEvent(commandType, id) { }
	virtual ~uMod_Event(void) {}

	// You *must* copy here the data to be transported
	uMod_Event(const uMod_Event& event)
		: wxCommandEvent(event) {
		this->SetText(event.GetText()); PipeIn = ((uMod_Event&)event).GetPipeIn(); PipeOut = ((uMod_Event&)event).GetPipeOut(); Name = ((uMod_Event&)event).GetName(); Client = ((uMod_Event&)event).GetClient();
	}

	// Required for sending with wxPostEvent()
	wxEvent* Clone() const { return new uMod_Event(*this); }

	wxString GetText() const { return m_Text; }
	void SetText(const wxString& text) { m_Text = text; }

	wxString GetName(void) { return Name; }
	HANDLE GetPipeIn(void) { return PipeIn; }
	HANDLE GetPipeOut(void) { return PipeOut; }
	uMod_Client* GetClient(void) { return Client; }

	void SetName(wxString name) { Name = name; }
	void SetPipeIn(HANDLE pipe) { PipeIn = pipe; }
	void SetPipeOut(HANDLE pipe) { PipeOut = pipe; }
	void SetClient(uMod_Client* client) { Client = client; }

private:

	wxString Name;
	HANDLE PipeIn;
	HANDLE PipeOut;
	uMod_Client* Client;

	wxString m_Text;
};


#endif /* uMod_EVENTTYPE_H_ */
