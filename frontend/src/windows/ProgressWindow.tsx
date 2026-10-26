// A running job's progress. Its events arrive here as the host sends them; the window closes when the job ends, unless the job has a
// finished message, which stays on screen until Done. A suspended launch pauses here until Continue, and Cancel is offered until the
// game resumes.

import { useEffect, useState } from 'react';
import { call } from '../bridge';
import { on } from '../events';
import { useAutosize } from '../autosize';
import { Button } from '../components/Button';
import { ProgressBar } from '../components/ProgressBar';
import { PathIcon } from '../components/PathIcon';

interface ProgressUpdate {
    phase: 'update' | 'paused' | 'end' | 'finished' | 'failed';
    fraction?: number | null;
    detail?: string;
    cancellable?: boolean;
    text?: string;
}

export function ProgressWindow({ heading, note, cancellable, icon, iconPath }: { heading: string; note?: string; cancellable?: boolean; icon?: string; iconPath?: string | null }) {
    const [root, setRoot] = useState<HTMLElement | null>(null);
    useAutosize(root);
    const [fraction, setFraction] = useState<number | null>(null);
    const [detail, setDetail] = useState('');
    const [failure, setFailure] = useState('');
    const [finished, setFinished] = useState('');
    const [cancelling, setCancelling] = useState(false);
    const [canCancel, setCanCancel] = useState(Boolean(cancellable));
    const [paused, setPaused] = useState(false);

    useEffect(() => on('progress', (p: ProgressUpdate) => {
        if (p.phase === 'update') {
            setPaused(false);
            setFraction(p.fraction ?? null);
            setDetail(p.detail ?? '');
            if (p.cancellable === false) {
                setCanCancel(false);
            }
        } else if (p.phase === 'paused') {
            setPaused(true);
        } else if (p.phase === 'end') {
            call('hide');
        } else if (p.phase === 'finished') {
            setFraction(1);
            setCanCancel(false);
            setFinished(p.text ?? 'Done');
        } else if (p.phase === 'failed') {
            setCanCancel(false);
            setFailure(p.text ?? '');
        }
    }), []);

    const cancel = () => {
        setCancelling(true);
        call('cancel');
    };
    const failed = failure !== '';
    return (
        <section ref={setRoot} className="view flex min-h-[190px] w-[420px] shrink-0 self-start flex-col gap-2.5 overflow-hidden p-3.5 [overflow-wrap:anywhere]">
            <h2 className="m-0 flex items-center gap-2 text-sm">
                {(icon || iconPath) && <PathIcon path={iconPath} fallback={icon} className="h-5 w-5 shrink-0" />}
                {heading}
            </h2>
            {note && <p className="note text-[11px] text-muted">{note}</p>}
            <ProgressBar fraction={fraction} label={heading} />
            <p className="note text-[11px] text-muted">{paused ? 'Launch paused.' : finished || detail}</p>
            {failed && <p className="error text-danger">{failure}</p>}
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                {failed ? (
                    <Button onClick={() => call('hide')}>Close</Button>
                ) : finished ? (
                    <Button autoFocus onClick={() => call('hide')}>
                        Done
                    </Button>
                ) : paused ? (
                    <>
                        <Button disabled={cancelling} onClick={cancel}>
                            {cancelling ? 'Cancelling…' : 'Cancel'}
                        </Button>
                        <Button
                            autoFocus
                            disabled={cancelling}
                            onClick={() => {
                                setPaused(false);
                                call('continueLaunch');
                            }}
                        >
                            Continue
                        </Button>
                    </>
                ) : (
                    canCancel && (
                        <Button disabled={cancelling} onClick={cancel}>
                            {cancelling ? 'Cancelling…' : 'Cancel'}
                        </Button>
                    )
                )}
            </div>
        </section>
    );
}
