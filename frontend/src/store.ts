// The launcher's state from the host, shared by every view. Reloaded when a view needs fresh flags.

import { useSyncExternalStore } from 'react';
import { call } from './bridge';
import type { AppState } from './types';

let current: AppState = {} as AppState;
const listeners = new Set<() => void>();

function notify() {
    listeners.forEach(listener => listener());
}

export function getState(): AppState {
    return current;
}

export async function refreshState(): Promise<AppState> {
    current = await call<AppState>('state');
    notify();
    return current;
}

// Replaces the state's settings after a save, as the page did with state.settings = settings.
export function setSettings(settings: AppState['settings']) {
    current = { ...current, settings };
    notify();
}

export function useAppState(): AppState {
    return useSyncExternalStore(
        listener => {
            listeners.add(listener);
            return () => listeners.delete(listener);
        },
        getState,
    );
}
