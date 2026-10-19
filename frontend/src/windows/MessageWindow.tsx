// A message: its text, a copy for a bug report, and OK.

import { call } from '../bridge';
import { useAppState } from '../store';
import { Button } from '../components/Button';
import { useState } from 'react';

export function MessageWindow({ title, text }: { title: string; text: string }) {
    const app = useAppState();
    const report = `${title}\n${text}\n\n${app.version ?? ''}`;
    const [copied, setCopied] = useState(false);
    return (
        <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5">
            <h2 className="m-0 text-sm">{title}</h2>
            <p className="error text-danger">{text}</p>
            <p className="note text-[11px] text-muted">{app.version ?? ''}</p>
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                <Button
                    onClick={() => {
                        call('copy', report);
                        setCopied(true);
                    }}
                >
                    {copied ? 'Copied' : 'Copy'}
                </Button>
                <Button onClick={() => call('hide')}>OK</Button>
            </div>
        </section>
    );
}
