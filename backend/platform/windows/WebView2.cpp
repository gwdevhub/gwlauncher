// The COM objects the Windows window hands to WebView2: its completed callbacks and its message event. Each is a reference-counted
// object that calls the function it's given, and WebView2 releases it when it's done.

#include "WebView2.h"

#include <atomic>
#include <utility>

#include "WebViewWindow.h"

namespace gwl {
namespace webview2 {

namespace {

template <typename Callback>
HRESULT invokeSafely(Callback callback) {
    try {
        callback();
        return S_OK;
    } catch (const std::exception& error) {
        runOnUiThread([message = std::string(error.what())] {
            showError("A WebView2 callback failed: " + message + ". Restart GW Launcher.");
            stopEventLoop();
        });
    } catch (...) {
        runOnUiThread([] {
            showError("A WebView2 callback failed with an unknown error. Restart GW Launcher.");
            stopEventLoop();
        });
    }
    return E_FAIL;
}

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
template <>
struct CallbackIid<ICoreWebView2NavigationCompletedEventHandler> {
    static const IID& value() { return IID_ICoreWebView2NavigationCompletedEventHandler; }
};
template <>
struct CallbackIid<ICoreWebView2ProcessFailedEventHandler> {
    static const IID& value() { return IID_ICoreWebView2ProcessFailedEventHandler; }
};

// A COM object for a WebView2 callback. The concrete handlers below supply each interface's Invoke.
template <typename Iface>
class ComObject : public Iface {
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** out) override {
        if (!out) return E_POINTER;
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

protected:
    virtual ~ComObject() = default;

private:
    std::atomic<ULONG> refs_{1};
};

class EnvironmentDone : public ComObject<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler> {
public:
    explicit EnvironmentDone(std::function<void(HRESULT, ICoreWebView2Environment*)> done) : done_(std::move(done)) {}
    HRESULT STDMETHODCALLTYPE Invoke(HRESULT result, ICoreWebView2Environment* environment) override {
        return invokeSafely([&] { done_(result, environment); });
    }

private:
    std::function<void(HRESULT, ICoreWebView2Environment*)> done_;
};

class ControllerDone : public ComObject<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler> {
public:
    explicit ControllerDone(std::function<void(HRESULT, ICoreWebView2Controller*)> done) : done_(std::move(done)) {}
    HRESULT STDMETHODCALLTYPE Invoke(HRESULT result, ICoreWebView2Controller* controller) override {
        return invokeSafely([&] { done_(result, controller); });
    }

private:
    std::function<void(HRESULT, ICoreWebView2Controller*)> done_;
};

class MessageReceived : public ComObject<ICoreWebView2WebMessageReceivedEventHandler> {
public:
    explicit MessageReceived(std::function<void(ICoreWebView2WebMessageReceivedEventArgs*)> received)
        : received_(std::move(received)) {}
    HRESULT STDMETHODCALLTYPE Invoke(ICoreWebView2* /*sender*/, ICoreWebView2WebMessageReceivedEventArgs* args) override {
        return invokeSafely([&] { received_(args); });
    }

private:
    std::function<void(ICoreWebView2WebMessageReceivedEventArgs*)> received_;
};

class NavigationDone : public ComObject<ICoreWebView2NavigationCompletedEventHandler> {
public:
    explicit NavigationDone(std::function<void(ICoreWebView2NavigationCompletedEventArgs*)> done) : done_(std::move(done)) {}
    HRESULT STDMETHODCALLTYPE Invoke(ICoreWebView2* /*sender*/, ICoreWebView2NavigationCompletedEventArgs* args) override {
        return invokeSafely([&] { done_(args); });
    }

private:
    std::function<void(ICoreWebView2NavigationCompletedEventArgs*)> done_;
};

class ProcessFailed : public ComObject<ICoreWebView2ProcessFailedEventHandler> {
public:
    explicit ProcessFailed(std::function<void(ICoreWebView2ProcessFailedEventArgs*)> failed) : failed_(std::move(failed)) {}
    HRESULT STDMETHODCALLTYPE Invoke(ICoreWebView2*, ICoreWebView2ProcessFailedEventArgs* args) override {
        return invokeSafely([&] { failed_(args); });
    }
private:
    std::function<void(ICoreWebView2ProcessFailedEventArgs*)> failed_;
};

class ScriptDone : public ComObject<ICoreWebView2ExecuteScriptCompletedHandler> {
public:
    explicit ScriptDone(std::function<void(HRESULT, LPCWSTR)> done) : done_(std::move(done)) {}
    HRESULT STDMETHODCALLTYPE Invoke(HRESULT result, LPCWSTR json) override {
        return invokeSafely([&] { done_(result, json); });
    }
private:
    std::function<void(HRESULT, LPCWSTR)> done_;
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

ICoreWebView2NavigationCompletedEventHandler* navigationDone(std::function<void(ICoreWebView2NavigationCompletedEventArgs*)> done) {
    return new NavigationDone(std::move(done));
}

ICoreWebView2ProcessFailedEventHandler* processFailed(std::function<void(ICoreWebView2ProcessFailedEventArgs*)> failed) {
    return new ProcessFailed(std::move(failed));
}

ICoreWebView2ExecuteScriptCompletedHandler* scriptDone(std::function<void(HRESULT, LPCWSTR)> done) {
    return new ScriptDone(std::move(done));
}

}  // namespace webview2
}  // namespace gwl
