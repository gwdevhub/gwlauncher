// Events the host pushes: navigation from the tray, progress for a dialog, refreshes and menu picks.
// Progress can arrive before its dialog has subscribed, so it's kept until a subscriber asks for it.

type Handler = (data: any) => void;

const handlers = new Map<string, Handler[]>();
const buffered = new Map<string, unknown[]>();

export function on(name: string, handler: Handler): () => void {
    const list = handlers.get(name) ?? [];
    list.push(handler);
    handlers.set(name, list);
    for (const data of buffered.get(name) ?? []) {
        handler(data);
    }
    buffered.delete(name);
    return () => handlers.set(name, (handlers.get(name) ?? []).filter(existing => existing !== handler));
}

export function emit(name: string, data: unknown) {
    const list = handlers.get(name) ?? [];
    if (list.length === 0) {
        if (name === 'progress') {
            buffered.set(name, [...(buffered.get(name) ?? []), data]);
        }
        return;
    }
    list.forEach(handler => handler(data));
}
