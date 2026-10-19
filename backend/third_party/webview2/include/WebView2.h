// A stand-in for the Microsoft.Web.WebView2 SDK's WebView2.h, with only what the launcher's Windows window calls.
//
// COM calls go through vtable slots, so every interface lists its methods in the SDK's order. Methods the launcher doesn't call
// are placeholders, so each method the launcher does call sits at the same slot as in the SDK:
//   ICoreWebView2Environment:  CreateCoreWebView2Controller                    slot 3
//   ICoreWebView2Controller:   put_IsVisible slot 4, put_Bounds slot 6, Close slot 24, get_CoreWebView2 slot 25
//   ICoreWebView2:             Navigate slot 5, NavigateToString slot 6, ExecuteScript slot 29, add_WebMessageReceived slot 34, OpenDevToolsWindow slot 51
//   ICoreWebView2WebMessageReceivedEventArgs: TryGetWebMessageAsString         slot 5
//   The four completed/event handler interfaces: Invoke                         slot 3
// The slots were read from the SDK's declarations. The GUIDs are the SDK's interface IDs.

#pragma once

#include <windows.h>
#include <unknwn.h>

struct ICoreWebView2;
struct ICoreWebView2Controller;
struct ICoreWebView2Environment;
struct ICoreWebView2EnvironmentOptions;
struct ICoreWebView2WebMessageReceivedEventArgs;

// ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler
static const IID IID_ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler = {0x4e8a3389,0xc9d8,0x4bd2,{0x12,0x4f,0xee,0x6c,0xc1,0x4d}};
// ICoreWebView2CreateCoreWebView2ControllerCompletedHandler
static const IID IID_ICoreWebView2CreateCoreWebView2ControllerCompletedHandler = {0x6c4819f3,0xc9b7,0x4260,{0xc9,0xf5,0xbd,0xe7,0xf6,0x8c}};
// ICoreWebView2WebMessageReceivedEventHandler
static const IID IID_ICoreWebView2WebMessageReceivedEventHandler = {0x57213f19,0x00e6,0x49fa,{0x89,0x8e,0xa0,0x1e,0xcb,0xd2}};
// ICoreWebView2ExecuteScriptCompletedHandler
static const IID IID_ICoreWebView2ExecuteScriptCompletedHandler = {0x49511172,0xcc67,0x4bca,{0x13,0x71,0x12,0xf4,0xc4,0xcc}};

// Token of an event subscription, as the SDK names it.
struct EventRegistrationToken {
    __int64 value;
};

struct ICoreWebView2Environment : public IUnknown {
    // Slots 0 to 2 are IUnknown's.
    virtual HRESULT STDMETHODCALLTYPE CreateCoreWebView2Controller(HWND parentWindow, struct ICoreWebView2CreateCoreWebView2ControllerCompletedHandler* handler) = 0;
};

struct ICoreWebView2Controller : public IUnknown {
    virtual void STDMETHODCALLTYPE Unused3() = 0;
    virtual HRESULT STDMETHODCALLTYPE put_IsVisible(BOOL isVisible) = 0;
    virtual void STDMETHODCALLTYPE Unused5() = 0;
    virtual HRESULT STDMETHODCALLTYPE put_Bounds(RECT bounds) = 0;
    virtual void STDMETHODCALLTYPE Unused7() = 0;
    virtual void STDMETHODCALLTYPE Unused8() = 0;
    virtual void STDMETHODCALLTYPE Unused9() = 0;
    virtual void STDMETHODCALLTYPE Unused10() = 0;
    virtual void STDMETHODCALLTYPE Unused11() = 0;
    virtual void STDMETHODCALLTYPE Unused12() = 0;
    virtual void STDMETHODCALLTYPE Unused13() = 0;
    virtual void STDMETHODCALLTYPE Unused14() = 0;
    virtual void STDMETHODCALLTYPE Unused15() = 0;
    virtual void STDMETHODCALLTYPE Unused16() = 0;
    virtual void STDMETHODCALLTYPE Unused17() = 0;
    virtual void STDMETHODCALLTYPE Unused18() = 0;
    virtual void STDMETHODCALLTYPE Unused19() = 0;
    virtual void STDMETHODCALLTYPE Unused20() = 0;
    virtual void STDMETHODCALLTYPE Unused21() = 0;
    virtual void STDMETHODCALLTYPE Unused22() = 0;
    virtual void STDMETHODCALLTYPE Unused23() = 0;
    virtual HRESULT STDMETHODCALLTYPE Close() = 0;
    virtual HRESULT STDMETHODCALLTYPE get_CoreWebView2(ICoreWebView2** coreWebView2) = 0;
};

struct ICoreWebView2 : public IUnknown {
    virtual void STDMETHODCALLTYPE Unused3() = 0;
    virtual void STDMETHODCALLTYPE Unused4() = 0;
    virtual HRESULT STDMETHODCALLTYPE Navigate(LPCWSTR uri) = 0;
    virtual HRESULT STDMETHODCALLTYPE NavigateToString(LPCWSTR htmlContent) = 0;
    virtual void STDMETHODCALLTYPE Unused7() = 0;
    virtual void STDMETHODCALLTYPE Unused8() = 0;
    virtual void STDMETHODCALLTYPE Unused9() = 0;
    virtual void STDMETHODCALLTYPE Unused10() = 0;
    virtual void STDMETHODCALLTYPE Unused11() = 0;
    virtual void STDMETHODCALLTYPE Unused12() = 0;
    virtual void STDMETHODCALLTYPE Unused13() = 0;
    virtual void STDMETHODCALLTYPE Unused14() = 0;
    virtual void STDMETHODCALLTYPE Unused15() = 0;
    virtual void STDMETHODCALLTYPE Unused16() = 0;
    virtual void STDMETHODCALLTYPE Unused17() = 0;
    virtual void STDMETHODCALLTYPE Unused18() = 0;
    virtual void STDMETHODCALLTYPE Unused19() = 0;
    virtual void STDMETHODCALLTYPE Unused20() = 0;
    virtual void STDMETHODCALLTYPE Unused21() = 0;
    virtual void STDMETHODCALLTYPE Unused22() = 0;
    virtual void STDMETHODCALLTYPE Unused23() = 0;
    virtual void STDMETHODCALLTYPE Unused24() = 0;
    virtual void STDMETHODCALLTYPE Unused25() = 0;
    virtual void STDMETHODCALLTYPE Unused26() = 0;
    virtual void STDMETHODCALLTYPE Unused27() = 0;
    virtual void STDMETHODCALLTYPE Unused28() = 0;
    virtual HRESULT STDMETHODCALLTYPE ExecuteScript(LPCWSTR javaScript, struct ICoreWebView2ExecuteScriptCompletedHandler* handler) = 0;
    virtual void STDMETHODCALLTYPE Unused30() = 0;
    virtual void STDMETHODCALLTYPE Unused31() = 0;
    virtual void STDMETHODCALLTYPE Unused32() = 0;
    virtual void STDMETHODCALLTYPE Unused33() = 0;
    virtual HRESULT STDMETHODCALLTYPE add_WebMessageReceived(struct ICoreWebView2WebMessageReceivedEventHandler* handler, EventRegistrationToken* token) = 0;
    virtual void STDMETHODCALLTYPE Unused35() = 0;
    virtual void STDMETHODCALLTYPE Unused36() = 0;
    virtual void STDMETHODCALLTYPE Unused37() = 0;
    virtual void STDMETHODCALLTYPE Unused38() = 0;
    virtual void STDMETHODCALLTYPE Unused39() = 0;
    virtual void STDMETHODCALLTYPE Unused40() = 0;
    virtual void STDMETHODCALLTYPE Unused41() = 0;
    virtual void STDMETHODCALLTYPE Unused42() = 0;
    virtual void STDMETHODCALLTYPE Unused43() = 0;
    virtual void STDMETHODCALLTYPE Unused44() = 0;
    virtual void STDMETHODCALLTYPE Unused45() = 0;
    virtual void STDMETHODCALLTYPE Unused46() = 0;
    virtual void STDMETHODCALLTYPE Unused47() = 0;
    virtual void STDMETHODCALLTYPE Unused48() = 0;
    virtual void STDMETHODCALLTYPE Unused49() = 0;
    virtual void STDMETHODCALLTYPE Unused50() = 0;
    virtual HRESULT STDMETHODCALLTYPE OpenDevToolsWindow() = 0;
};

struct ICoreWebView2WebMessageReceivedEventArgs : public IUnknown {
    virtual void STDMETHODCALLTYPE Unused3() = 0;
    virtual void STDMETHODCALLTYPE Unused4() = 0;
    virtual HRESULT STDMETHODCALLTYPE TryGetWebMessageAsString(LPWSTR* webMessageAsString) = 0;
};

struct ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler : public IUnknown {
    virtual HRESULT Invoke(HRESULT errorCode, ICoreWebView2Environment* result) = 0;
};

struct ICoreWebView2CreateCoreWebView2ControllerCompletedHandler : public IUnknown {
    virtual HRESULT Invoke(HRESULT errorCode, ICoreWebView2Controller* result) = 0;
};

struct ICoreWebView2WebMessageReceivedEventHandler : public IUnknown {
    virtual HRESULT Invoke(ICoreWebView2* sender, ICoreWebView2WebMessageReceivedEventArgs* args) = 0;
};

struct ICoreWebView2ExecuteScriptCompletedHandler : public IUnknown {
    virtual HRESULT Invoke(HRESULT errorCode, LPCWSTR result) = 0;
};

