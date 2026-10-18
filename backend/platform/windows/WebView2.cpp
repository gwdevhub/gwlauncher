// The COM objects the Windows window hands to WebView2: its completed callbacks and its message event. Each is a reference-counted
// object that calls the function it's given, and WebView2 releases it when it's done.

#include "WebView2.h"

#include <utility>

namespace gwl {
namespace webview2 {

namespace {

// The interface IDs of the WebView2 callbacks. MinGW can't give these types a __uuidof, so they're named here.
template <typename Iface>
struct CallbackIid;
template <>
struct CallbackIid<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler> {
    static const IID& value() { return IID_ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler; }
};
template <>
struct CallbackIid<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler> {
    static const IID& value() { return IID_ICoreWebView2CreateCoreWebView2ControllerCompletedHandler; }
};
template <>
struct CallbackIid<ICoreWebView2WebMessageReceivedEventHandler> {
    static const IID& value() { return IID_ICoreWebView2WebMessageReceivedEventHandler; }
};
template <>
struct CallbackIid<ICoreWebView2ExecuteScriptCompletedHandler> {
    static const IID& value() { return IID_ICoreWebView2ExecuteScriptCompletedHandler; }
};

// A COM object for a WebView2 callback. The concrete handlers below supply each interface's Invoke.
template <typename Iface>
class ComObject : public Iface {
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** out) override {
        if (riid == IID_IUnknown || riid == CallbackIid<Iface>::value()) {
            *out = static_cast<Iface*>(this);
            AddRef();
            return S_OK;
        }
        *out = nullptr;
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++refs_; }
    ULONG STDMETHODCALLTYPE Release() override {
        ULONG left = --refs_;
        if (left == 0) {
            delete this;
        }
        return left;
    }

private:
    ULONG refs_ = 1;
};

class EnvironmentDone : public ComObject<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler> {
public:
    explicit EnvironmentDone(std::function<void(HRESULT, ICoreWebView2Environment*)> done) : done_(std::move(done)) {}
    HRESULT STDMETHODCALLTYPE Invoke(HRESULT result, ICoreWebView2Environment* environment) override {
        done_(result, environment);
        return S_OK;
    }

private:
    std::function<void(HRESULT, ICoreWebView2Environment*)> done_;
};

class ControllerDone : public ComObject<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler> {
public:
    explicit ControllerDone(std::function<void(HRESULT, ICoreWebView2Controller*)> done) : done_(std::move(done)) {}
    HRESULT STDMETHODCALLTYPE Invoke(HRESULT result, ICoreWebView2Controller* controller) override {
        done_(result, controller);
        return S_OK;
    }

private:
    std::function<void(HRESULT, ICoreWebView2Controller*)> done_;
};

class MessageReceived : public ComObject<ICoreWebView2WebMessageReceivedEventHandler> {
public:
    explicit MessageReceived(std::function<void(ICoreWebView2WebMessageReceivedEventArgs*)> received)
        : received_(std::move(received)) {}
    HRESULT STDMETHODCALLTYPE Invoke(ICoreWebView2* /*sender*/, ICoreWebView2WebMessageReceivedEventArgs* args) override {
        received_(args);
        return S_OK;
    }

private:
    std::function<void(ICoreWebView2WebMessageReceivedEventArgs*)> received_;
};

class ScriptDone : public ComObject<ICoreWebView2ExecuteScriptCompletedHandler> {
public:
    HRESULT STDMETHODCALLTYPE Invoke(HRESULT /*result*/, LPCWSTR /*json*/) override { return S_OK; }
};

}  // namespace

ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler* environmentDone(std::function<void(HRESULT, ICoreWebView2Environment*)> done) {
    return new EnvironmentDone(std::move(done));
}

ICoreWebView2CreateCoreWebView2ControllerCompletedHandler* controllerDone(std::function<void(HRESULT, ICoreWebView2Controller*)> done) {
    return new ControllerDone(std::move(done));
}

ICoreWebView2WebMessageReceivedEventHandler* messageReceived(std::function<void(ICoreWebView2WebMessageReceivedEventArgs*)> received) {
    return new MessageReceived(std::move(received));
}

ICoreWebView2ExecuteScriptCompletedHandler* scriptDone() {
    return new ScriptDone();
}

}  // namespace webview2
}  // namespace gwl
