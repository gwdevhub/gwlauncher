// Prerequisites the platform installs, one at a time. A running install keeps going if the window closes.

import { useEffect, useState } from 'react';
import { call } from '../bridge';
import { showMessage } from '../dialogs';
import { show } from '../nav';
import { Button } from '../components/Button';
import { Note } from '../components/Note';
import { ProgressBar } from '../components/ProgressBar';

interface SetupItem {
    title: string;
    description: string;
    done: boolean;
    canInstall: boolean;
    folder?: string;
}

interface SetupProgress {
    running: boolean;
    lastLine: string;
    error: string | null;
}

export function SetupWindow() {
    const [items, setItems] = useState<SetupItem[] | null>(null);
    const [progress, setProgress] = useState<SetupProgress | null>(null);
    const [folder, setFolder] = useState('');

    const load = async () => {
        const [loaded, status] = await Promise.all([call<SetupItem[]>('setup'), call<SetupProgress>('setupStatus')]);
        setItems(loaded);
        setProgress(status);
        const next = loaded.find(item => !item.done);
        setFolder(next?.folder ?? '');
    };

    useEffect(() => {
        load();
    }, []);

    // While an install runs, its log is followed; when it ends, the view is drawn again.
    useEffect(() => {
        if (!progress?.running) {
            return;
        }
        const timer = setInterval(async () => {
            const now = await call<SetupProgress>('setupStatus');
            if (!now.running) {
                clearInterval(timer);
                show('setup');
            } else {
                setProgress(now);
            }
        }, 500);
        return () => clearInterval(timer);
    }, [progress?.running]);

    if (!items || !progress) {
        return <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5" />;
    }

    const next = items.findIndex(item => !item.done);
    const complete = next < 0;
    const current = items[next] as SetupItem | undefined;
    if (!current && !complete) {
        return <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5" />;
    }

    let message = '';
    if (!complete && progress.running) {
        message = 'Installation continues if this window is closed.';
    } else if (!complete && !current?.canInstall) {
        message = `Install ${current?.title ?? ''} with your package manager, then reopen setup.`;
    }

    return (
        <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5">
            <h2 className="m-0 text-sm">{complete ? 'Setup complete' : 'Setup'}</h2>
            <ul className="steps m-0 list-none p-0">
                {items.map((item, i) => (
                    <li key={item.title} className={`flex gap-2.5 mb-2 ${item.done ? 'done' : i === next ? '' : 'later opacity-50'}`}>
                        <span className={`mark w-4 text-[15px] text-muted ${item.done ? '!text-ok' : ''}`}>{item.done ? '✓' : '○'}</span>
                        <div>
                            <strong>{item.title}</strong>
                            <p className="note text-[11px] text-muted">{item.description}</p>
                        </div>
                    </li>
                ))}
            </ul>
            {message && <p>{message}</p>}
            {progress.error && <p className="error text-danger">{progress.error}</p>}
            {progress.running && <ProgressBar fraction={null} label="Installing" />}
            {progress.running && <Note>{progress.lastLine}</Note>}
            {!complete && current?.folder && (
                <label className="row form-grid-wide grid items-center gap-2">
                    <span>Install to:</span>
                    <input className="text-input w-full select-text rounded border border-border px-1.5 py-1" type="text" value={folder} disabled={progress.running} onChange={e => setFolder(e.target.value)} />
                </label>
            )}
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                {!complete && (
                    <Button
                        disabled={progress.running || !current?.canInstall}
                        onClick={async () => {
                            const error = await call<string | null>('startSetup', current?.folder ? folder.trim() : null);
                            if (error) {
                                showMessage('Setup', error);
                            }
                            show('setup');
                        }}
                    >
                        {`Install ${current?.title ?? ''}`}
                    </Button>
                )}
                <Button onClick={() => show('list')}>Close</Button>
            </div>
        </section>
    );
}
