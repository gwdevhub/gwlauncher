// The account editor: the account's fields on the left, its mods on the right. Save sends the draft to the host.

import { useEffect, useState, type ReactNode } from 'react';
import { call } from '../bridge';
import { Button } from '../components/Button';
import { Check } from '../components/Check';
import { Note } from '../components/Note';
import { show } from '../nav';
import { carryListState } from '../status';
import { useAppState } from '../store';
import type { AccountDraft } from '../types';
import { ModsPane } from '../components/ModsPane';

// The field each save error is about, for the errors the host sends as plain text.
const invalidFields: Record<string, string> = {
    'Enter a title for the account.': 'title',
    'Enter a path to Gw.exe.': 'gwPath',
    'Gw.exe was not found at that path.': 'gwPath',
};

type FieldKey = 'title' | 'email' | 'password' | 'character' | 'gwPath' | 'extraArgs';

async function accountNames(): Promise<string[]> {
    const { entries } = await call<{ entries: { name: string }[] }>('accounts');
    return entries.map(entry => entry.name);
}

// The base name, or the base with the first free " (N)" after it when an account already has that name.
function uniqueTitle(base: string, names: string[]): string {
    if (!names.includes(base)) {
        return base;
    }
    let n = 2;
    while (names.includes(`${base} (${n})`)) {
        n++;
    }
    return `${base} (${n})`;
}

interface SaveFailure {
    message: string;
    fields?: string[];
}

export function AccountEditWindow({ index, keepDraft, copyOf }: { index: number | null; keepDraft?: boolean; copyOf?: AccountDraft | null }) {
    const app = useAppState();
    const [draft, setDraft] = useState<AccountDraft | null>(copyOf ?? null);
    const [editing] = useState<number | null>(copyOf ? null : index);
    const [error, setError] = useState('');
    const [invalid, setInvalid] = useState<string[]>([]);

    useEffect(() => {
        if (copyOf || keepDraft) {
            return;
        }
        call<AccountDraft>('draft', index).then(async loaded => {
            if (index == null && !loaded.title) {
                const names = await accountNames();
                loaded.title = uniqueTitle(`Account ${names.length + 1}`, names);
            }
            setDraft(loaded);
        });
    }, [copyOf, keepDraft, index]);

    if (!draft) {
        return <section />;
    }

    const update = (patch: Partial<AccountDraft>) => setDraft({ ...draft, ...patch });
    const text = (key: FieldKey, label: string, options: { type?: string; required?: boolean; browse?: boolean } = {}) => (
        <Row label={label} required={options.required} trailing={options.browse ? <Browse update={update} gwPath={draft.gwPath} /> : undefined}>
            <input
                type={options.type ?? 'text'}
                value={draft[key] ?? ''}
                className={`text-input w-full select-text rounded border border-border px-1.5 py-1 ${invalid.includes(key) ? 'invalid' : ''}`}
                onChange={e => {
                    setInvalid(invalid.filter(name => name !== key));
                    update({ [key]: e.target.value } as Partial<AccountDraft>);
                }}
            />
        </Row>
    );

    const save = async () => {
        const { entries } = await call<{ entries: unknown[] }>('accounts');
        const result = await call<string | SaveFailure | null>('save', editing, draft);
        const failure = typeof result === 'string' ? { message: result, fields: [invalidFields[result]].filter(Boolean) } : result;
        if (failure?.message) {
            setError(failure.message);
            setInvalid(failure.fields ?? []);
            return;
        }
        carryListState({ selected: editing ?? entries.length });
        show('list');
    };

    const duplicate = async () => {
        const copy: AccountDraft = JSON.parse(JSON.stringify(draft));
        copy.title = uniqueTitle(draft.title, await accountNames());
        call('openWindow', `editor-copy:${Date.now()}`, 'editor', [null, false, copy]);
    };

    const isNew = editing == null;
    return (
        <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5">
            <div className="split flex min-h-0 flex-1">
                <div className="pane fields flex w-[360px] flex-none flex-col gap-2.5 overflow-auto">
                    <h2 className="m-0 text-sm">{isNew ? 'Add Account' : 'Modify Account'}</h2>
                    {text('title', 'Title:', { required: true })}
                    <div hidden={draft.steamAccount} className="flex flex-col gap-2.5">
                        {text('email', 'Email:')}
                        {text('password', 'Pass:', { type: 'password' })}
                        {text('character', 'Char:')}
                    </div>
                    {text('gwPath', 'Path:', { required: true, browse: true })}
                    {text('extraArgs', 'Ex Args:')}
                    {app.hasWindowsOptions && (
                        <Check
                            label="Steam account"
                            checked={draft.steamAccount}
                            onChange={e => update({ steamAccount: e.target.checked })}
                        />
                    )}
                    {app.hasWindowsOptions && (
                        <Check
                            label="Run as administrator"
                            checked={draft.elevated}
                            onChange={e => update({ elevated: e.target.checked })}
                        />
                    )}
                </div>
                <div className="divider mx-3.5 w-px bg-border" />
                <ModsPane draft={draft} update={update} />
            </div>
            {error && <p className="error">{error}</p>}
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                {!isNew && <Button className="left mr-auto" onClick={duplicate}>Duplicate</Button>}
                <Button onClick={() => show('list')}>Cancel</Button>
                <Button onClick={save}>Save</Button>
            </div>
        </section>
    );
}

function Row({ label, required, trailing, children }: { label: string; required?: boolean; trailing?: ReactNode; children: ReactNode }) {
    return (
        <label className="row form-grid grid items-center gap-2">
            <span>
                {label}
                {required && <span className="required"> *</span>}
            </span>
            {children}
            {trailing ?? <span />}
        </label>
    );
}

// Picks the Gw.exe for the draft's path.
function Browse({ update, gwPath }: { update: (patch: Partial<AccountDraft>) => void; gwPath: string }) {
    return (
        <Button
            className="small"
            onClick={async () => {
                const path = await call<string | null>('pickFile', 'Select Gw.exe', false, gwPath || null);
                if (path) {
                    update({ gwPath: path });
                }
            }}
        >
            ...
        </Button>
    );
}
