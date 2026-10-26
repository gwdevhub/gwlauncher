// The WebView2 callbacks the Windows window hands to WebView2. The objects themselves are in WebView2.cpp.

#pragma once

#include <windows.h>

#include <WebView2.h>

#include <functional>
#include "ComPtr.h"

namespace gwl {
namespace webview2 {

ComPtr<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler> environmentDone(std::function<void(HRESULT, ICoreWebView2Environment*)> done);
ComPtr<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler> controllerDone(std::function<void(HRESULT, ICoreWebView2Controller*)> done);
ComPtr<ICoreWebView2WebMessageReceivedEventHandler> messageReceived(std::function<void(ICoreWebView2WebMessageReceivedEventArgs*)> received);
ComPtr<ICoreWebView2NavigationCompletedEventHandler> navigationDone(std::function<void(ICoreWebView2NavigationCompletedEventArgs*)> done);
ComPtr<ICoreWebView2ProcessFailedEventHandler> processFailed(std::function<void(ICoreWebView2ProcessFailedEventArgs*)> failed);
ComPtr<ICoreWebView2ExecuteScriptCompletedHandler> scriptDone(std::function<void(HRESULT, LPCWSTR)> done);

}  // namespace webview2
}  // namespace gwl
