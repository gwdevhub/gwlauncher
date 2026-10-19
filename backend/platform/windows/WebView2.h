// The WebView2 callbacks the Windows window hands to WebView2. The objects themselves are in WebView2.cpp.

#pragma once

#include <windows.h>

#include <WebView2.h>

#include <functional>

namespace gwl {
namespace webview2 {

// Each makes a callback with one reference, for WebView2 to release when it's done with it.
ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler* environmentDone(std::function<void(HRESULT, ICoreWebView2Environment*)> done);
ICoreWebView2CreateCoreWebView2ControllerCompletedHandler* controllerDone(std::function<void(HRESULT, ICoreWebView2Controller*)> done);
ICoreWebView2WebMessageReceivedEventHandler* messageReceived(std::function<void(ICoreWebView2WebMessageReceivedEventArgs*)> received);
ICoreWebView2ExecuteScriptCompletedHandler* scriptDone();

}  // namespace webview2
}  // namespace gwl
