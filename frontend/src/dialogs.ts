// Dialogs the host opens: messages and choices. Each is its own window; the host returns the answer.

import { call } from './bridge';

export function showMessage(title: string, text: string) {
    call('message', title, text);
}

export function choose(title: string, message: string, options: string[]): Promise<number | null> {
    return call<number | null>('choose', title, message, options);
}
