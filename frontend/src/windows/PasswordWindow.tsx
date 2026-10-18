// The unlock prompt for an encrypted Accounts.json.

import { useState, type KeyboardEvent } from 'react';
import { call } from '../bridge';
import { show } from '../nav';
import { useAppState } from '../store';
import { Button } from '../components/Button';
import { Check } from '../components/Check';

export function PasswordWindow() {
    const app = useAppState();
    const [password, setPassword] = useState('');
    const [remember, setRemember] = useState(false);
    const [error, setError] = useState('');

    const submit = async () => {
        if (!password) {
            setError('Enter the master password.');
            return;
        }
        const message = await call<string | null>('unlock', password, remember);
        if (message) {
            setError(message);
        } else {
            show('list');
        }
    };

    return (
        <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5">
            <p>Accounts are encrypted. Enter the master password to open them.</p>
            <input
                className="text-input w-full select-text rounded border border-border px-1.5 py-1"
                type="password"
                placeholder="Master password"
                autoFocus
                value={password}
                onChange={e => setPassword(e.target.value)}
                onKeyDown={(e: KeyboardEvent) => {
                    if (e.key === 'Enter') {
                        submit();
                    }
                }}
            />
            {app.hasWindowsOptions && <Check label="Don't ask again on this computer" checked={remember} onChange={e => setRemember(e.target.checked)} />}
            {error && <p className="error text-danger">{error}</p>}
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                <Button onClick={() => call('quit')}>Quit</Button>
                <Button onClick={submit}>OK</Button>
            </div>
        </section>
    );
}
