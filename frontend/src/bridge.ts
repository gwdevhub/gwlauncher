// The page's link to the host. call() sends a request and resolves with the host's reply. The host's events arrive
// through window.gwlReceive and go to the event bus.

import { emit } from './events';

declare global {
    interface Window {
        chrome?: { webview?: { postMessage(text: string): void } };
        webkit?: { messageHandlers?: { gwl?: { postMessage(text: string): void } } };
        ipc?: { postMessage(text: string): void };
        gwlReceive: (message: HostMessage) => void;
    }
}

interface HostMessage {
    id?: number;
    event?: string;
    data?: unknown;
    result?: unknown;
    error?: string;
}

const pending = new Map<number, { resolve: (value: unknown) => void; reject: (error: Error) => void }>();
let nextRequest = 1;

function post(text: string) {
    if (window.chrome?.webview) {
        window.chrome.webview.postMessage(text);
    } else if (window.webkit?.messageHandlers?.gwl) {
        window.webkit.messageHandlers.gwl.postMessage(text);
    } else {
        // wry's own channel, used by the Rust launcher on WebKitGTK.
        window.ipc?.postMessage(text);
    }
}

export function call<T = any>(method: string, ...args: unknown[]): Promise<T> {
    const id = nextRequest++;
    return new Promise<T>((resolve, reject) => {
        pending.set(id, { resolve: resolve as (value: unknown) => void, reject });
        post(JSON.stringify({ id, method, args }));
    });
}

window.gwlReceive = message => {
    if (message.event) {
        emit(message.event, message.data);
        return;
    }

    const request = message.id !== undefined ? pending.get(message.id) : undefined;
    if (message.id !== undefined) {
        pending.delete(message.id);
    }
    if (message.error !== undefined) {
        request?.reject(new Error(message.error));
    } else {
        request?.resolve(message.result);
    }
};
