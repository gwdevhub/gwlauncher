// Navigation between views. The list is the main window's view. Editor, issues, settings and setup open in windows of
// their own from the list; every other view is a window the host opened for it, which shows only that view.

import { call } from './bridge';
import type { Role } from './types';

// Each view's window size, in CSS pixels. Forms are pinned so the window doesn't hide when focus moves to a file picker.
const sizes: Record<string, [number, number]> = {
    editor: [820, 500],
    issues: [470, 380],
    settings: [420, 600],
    password: [380, 240],
    setup: [460, 420],
    message: [420, 220],
    choose: [420, 200],
    progress: [420, 200],
    update: [460, 380],
};

const windowed = new Set(['editor', 'issues', 'settings', 'setup']);

export interface ViewState {
    name: string;
    args: any[];
    key: number;
}

export const session: { popup: Role | null; current: string } = { popup: null, current: '' };

let setView: ((view: ViewState) => void) | null = null;
let counter = 0;

export function bindViewSetter(setter: (view: ViewState) => void) {
    setView = setter;
}

// Shows a view. Popups hide instead of showing the list; the list opens in its own window when asked from a popup.
export async function show(name: string, ...args: any[]) {
    if (session.popup && name === 'list') {
        call('hide');
        return;
    }

    if (!session.popup && windowed.has(name)) {
        call('openWindow', `${name}:${JSON.stringify(args)}`, name, args);
        return;
    }

    call('pin', name !== 'list');
    const size = session.popup ? sizes[name] : await call<[number, number]>('listSize');
    call('resize', ...size);
    showNow(name, args);
}

// Shows a view with no window work first, for the menu, which sizes itself after it's drawn.
export function showNow(name: string, args: any[]) {
    session.current = name;
    setView?.({ name, args, key: ++counter });
}

// Opens a view the host asked for: from the tray menu, or after a desktop shortcut couldn't launch.
export function openView(view: { view: string; title?: string; text?: string; issues?: unknown[] }, showMessage: (title: string, text: string) => void) {
    if (view.view === 'issues') {
        show('issues', view);
    } else if (view.view === 'message') {
        show('list').then(() => showMessage(view.title ?? '', view.text ?? ''));
    } else {
        show(view.view);
    }
}
