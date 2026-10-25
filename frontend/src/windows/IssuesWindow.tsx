// The account's issues. Blocking issues stop a launch; Fix runs the fix an issue offers, and Launch unlocks once no
// blocking issue is left.

import { useEffect, useState } from 'react';
import { call } from '../bridge';
import { show } from '../nav';
import { choose } from '../dialogs';
import type { Issue } from '../types';
import { Button } from '../components/Button';
import { Note } from '../components/Note';
import { ProgressBar } from '../components/ProgressBar';

export interface IssuesRequest {
    index: number;
    name: string;
    issues: Issue[] | null;
    canLaunch: boolean;
    launchBlocked: boolean;
}

export function IssuesWindow({ view }: { view: IssuesRequest }) {
    const [issues, setIssues] = useState<Issue[] | null>(view.issues);
    const [failure, setFailure] = useState('');
    const [line, setLine] = useState('');

    // Without issues the account is checked first; the window shows a progress indicator meanwhile.
    useEffect(() => {
        if (view.issues) {
            return;
        }
        call<Issue[]>('issues', view.index)
            .then(setIssues)
            .catch((error: Error) => setFailure(error.message));
        // Checked once per opening of the window.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    // Checks again, for a problem the user has fixed outside the launcher. The list is replaced while it runs.
    const recheck = () => {
        setFailure('');
        setIssues(null);
        call<Issue[]>('issues', view.index)
            .then(setIssues)
            .catch((error: Error) => setFailure(error.message));
    };

    if (!issues) {
        return (
            <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5">
                <h2 className="m-0 text-sm">{`Issues: ${view.name}`}</h2>
                <Note>Checking…</Note>
                {failure ? <p className="error text-danger">{failure}</p> : <ProgressBar fraction={null} label="Checking the account" />}
                <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                    <Button onClick={() => show('list')}>Close</Button>
                </div>
            </section>
        );
    }

    return <IssueList view={view} issues={issues} line={line} setLine={setLine} onRecheck={recheck} />;
}

function IssueList({
    view,
    issues,
    line,
    setLine,
    onRecheck,
}: {
    view: IssuesRequest;
    issues: Issue[];
    line: string;
    setLine: (line: string) => void;
    onRecheck: () => void;
}) {
    const [busy, setBusy] = useState<Issue | null>(null);

    const sorted = [...issues].sort((a, b) => a.severity - b.severity);

    const fix = async (issue: Issue) => {
        if (issue.wofPath) {
            // The decompressed file replaces the original, so the user confirms first.
            const name = issue.wofPath.split(/[\\/]/).pop() ?? issue.wofPath;
            const answer = await choose(
                'Decompress file',
                `Decompress ${name}? This replaces the compressed original.`,
                ['Decompress', 'Cancel'],
            );
            if (answer !== 0) {
                return;
            }
        }
        setBusy(issue);
        const message = await call<string | null>('fix', view.index, issue);
        setLine(message ?? 'Nothing was changed.');
        setBusy(null);
        if (message != null) {
            onRecheck();
        }
    };

    return (
        <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5">
            <h2 className="m-0 text-sm">{view.launchBlocked ? `Can't launch ${view.name}` : `Issues: ${view.name}`}</h2>
            {issues.length === 0 && <p>No issues found.</p>}
            {sorted.map((issue, i) => {
                const blocking = issue.severity === 0;
                const fixable = Boolean(
                    issue.wofPath ||
                        issue.canDownloadGMod ||
                        issue.canUpdateGwExe ||
                        issue.canInstallSteam ||
                        issue.canInstallProton ||
                        issue.canInstallWine,
                );
                return (
                    <div key={i}>
                        <p className={blocking ? 'error text-danger' : 'warning text-warning'}>{(blocking ? 'Blocking: ' : 'Warning: ') + issue.message}</p>
                        {fixable && (
                            <Button className="small fix mt-2.5 block" disabled={busy === issue} onClick={() => fix(issue)}>
                                Fix
                            </Button>
                        )}
                    </div>
                );
            })}
            <Note>{line}</Note>
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                <Button className="mr-auto" onClick={onRecheck}>
                    Re-check
                </Button>
                <Button
                    disabled={issues.length > 0}
                    onClick={async () => {
                        const error = await call<string | null>('launchNow', view.index);
                        if (error) {
                            setLine(`Can't start ${view.name}: ${error}`);
                        } else {
                            show('list');
                        }
                    }}
                >
                    Launch Now
                </Button>
                <Button onClick={() => show('list')}>Close</Button>
            </div>
        </section>
    );
}
