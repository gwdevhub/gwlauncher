// The window's chrome from the host: its title, whether the page draws a title bar, and whether its edges resize it.

import { useSyncExternalStore } from 'react';
import { call } from './bridge';
import { on } from './events';
import type { Chrome } from './types';

let current: Chrome = { title: '', titleBar: false, resizable: false };
const listeners = new Set<() => void>();

function setChrome(next: Chrome) {
    current = next;
    listeners.forEach(listener => listener());
}

export function getChrome(): Chrome {
    return current;
}

// Asked for once at start. The host pushes changes after that, such as the list's title bar following the tray setting.
export async function loadChrome() {
    on('chrome', (next: Chrome) => setChrome(next));
    setChrome(await call<Chrome>('chrome'));
}

export function useChrome(): Chrome {
    return useSyncExternalStore(
        listener => {
            listeners.add(listener);
            return () => listeners.delete(listener);
        },
        getChrome,
    );
}
