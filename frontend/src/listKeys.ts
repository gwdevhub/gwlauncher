// The account list registers its keyboard handlers here, so the page's key handler can reach them while the list shows.

export interface ListKeys {
    ArrowDown: () => void;
    ArrowUp: () => void;
    Enter: (e: KeyboardEvent) => void;
    Delete: () => void;
}

let handlers: ListKeys | null = null;

export function setListKeys(next: ListKeys | null) {
    handlers = next;
}

export function listKey(key: string): ((e: KeyboardEvent) => void) | null {
    if (!handlers) {
        return null;
    }
    const handler = handlers[key as keyof ListKeys];
    return handler ? (e: KeyboardEvent) => handler(e) : null;
}
