// WebView2 retains submitted callbacks; ComPtr owns the caller's reference.

#include "WebView2.h"

#include <atomic>
#include <utility>

#include "WebViewWindow.h"
#include "utils/Errors.h"

namespace gwl::webview2 {
namespace {

template <typename Interface, typename... Args>
class Callback final : public Interface {
public:
    Callback(const IID& iid, std::function<void(Args...)> invoke) : iid_(iid), invoke_(std::move(invoke)) {}

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** out) override {
        if (!out) return E_POINTER;
        *out = iid == IID_IUnknown || iid == iid_ ? static_cast<Interface*>(this) : nullptr;
        if (!*out) return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++refs_; }
    ULONG STDMETHODCALLTYPE Release() override {
        const ULONG left = --refs_;
        if (!left) delete this;
        return left;
    }
    HRESULT STDMETHODCALLTYPE Invoke(Args... args) override {
        const auto error = captureError([&] { invoke_(args...); }, "A WebView2 callback failed: ");
        if (error.empty()) return S_OK;
        reportFatalError(error);
        return E_FAIL;
    }

private:
    ~Callback() = default;
    const IID iid_;
    std::function<void(Args...)> invoke_;
    std::atomic<ULONG> refs_{1};
};

template <typename Interface, typename... Args>
ComPtr<Interface> callback(const IID& iid, std::function<void(Args...)> invoke) {
    return ComPtr<Interface>(new Callback<Interface, Args...>(iid, std::move(invoke)));
}

template <typename Interface, typename Args>
ComPtr<Interface> eventHandler(const IID& iid, std::function<void(Args*)> invoke) {
    return ComPtr<Interface>(new Callback<Interface, ICoreWebView2*, Args*>(
        iid, [invoke = std::move(invoke)](ICoreWebView2*, Args* args) { invoke(args); }));
}

}  // namespace

ComPtr<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler> environmentDone(std::function<void(HRESULT, ICoreWebView2Environment*)> done) {
    return callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>(IID_ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler, std::move(done));
}

ComPtr<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler> controllerDone(std::function<void(HRESULT, ICoreWebView2Controller*)> done) {
    return callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(IID_ICoreWebView2CreateCoreWebView2ControllerCompletedHandler, std::move(done));
}

ComPtr<ICoreWebView2WebMessageReceivedEventHandler> messageReceived(std::function<void(ICoreWebView2WebMessageReceivedEventArgs*)> received) {
    return eventHandler<ICoreWebView2WebMessageReceivedEventHandler>(IID_ICoreWebView2WebMessageReceivedEventHandler, std::move(received));
}

ComPtr<ICoreWebView2NavigationCompletedEventHandler> navigationDone(std::function<void(ICoreWebView2NavigationCompletedEventArgs*)> done) {
    return eventHandler<ICoreWebView2NavigationCompletedEventHandler>(IID_ICoreWebView2NavigationCompletedEventHandler, std::move(done));
}

ComPtr<ICoreWebView2ProcessFailedEventHandler> processFailed(std::function<void(ICoreWebView2ProcessFailedEventArgs*)> failed) {
    return eventHandler<ICoreWebView2ProcessFailedEventHandler>(IID_ICoreWebView2ProcessFailedEventHandler, std::move(failed));
}

ComPtr<ICoreWebView2ExecuteScriptCompletedHandler> scriptDone(std::function<void(HRESULT, LPCWSTR)> done) {
    return callback<ICoreWebView2ExecuteScriptCompletedHandler>(IID_ICoreWebView2ExecuteScriptCompletedHandler, std::move(done));
}

}  // namespace gwl::webview2
