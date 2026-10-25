// The update prompt: the release notes and a link to the release. Not now is focused, so Enter declines.

import { call } from '../bridge';
import { Button } from '../components/Button';

export function UpdateWindow({ id, title, message, notes, releaseUrl }: { id: number; title: string; message: string; notes: string; releaseUrl?: string }) {
    return (
        <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5">
            <h2 className="m-0 text-sm">{title}</h2>
            <p>{message}</p>
            <p className="note text-[11px] text-muted">What's new:</p>
            <pre className="notes m-0 min-h-0 flex-1 select-text overflow-auto whitespace-pre-wrap rounded border border-border p-2 font-[inherit]">{notes}</pre>
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                {releaseUrl && (
                    <Button className="left mr-auto" onClick={() => call('openUrl', releaseUrl)}>
                        View release
                    </Button>
                )}
                <Button
                    autoFocus
                    onClick={() => {
                        call('answer', id, 0);
                        call('hide');
                    }}
                >
                    Not now
                </Button>
                <Button
                    onClick={() => {
                        call('answer', id, 1);
                        call('hide');
                    }}
                >
                    Update
                </Button>
            </div>
        </section>
    );
}
