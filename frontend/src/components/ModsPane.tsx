// The mod list beside the editor's fields. Plugin-folder files are listed read-only while the account uses them.

import { useEffect, useState } from 'react';
import { call } from '../bridge';
import { Button } from './Button';
import { Check } from './Check';
import { DLL, fileName, isDll } from '../utils/format';
import type { AccountDraft, ModDraft } from '../types';

export function ModsPane({ draft, update }: { draft: AccountDraft; update: (patch: Partial<AccountDraft>) => void }) {
    const [plugins, setPlugins] = useState<ModDraft[]>([]);
    const [picked, setPicked] = useState<Set<string>>(new Set());
    const [message, setMessage] = useState('');

    // Plugin-folder files depend on the game's folder and on whether the account uses them.
    useEffect(() => {
        if (!draft.usePluginFolderMods) {
            setPlugins([]);
            return;
        }
        call<ModDraft[]>('pluginFiles', draft).then(setPlugins);
        // Only the switch and the path change which files are listed.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [draft.usePluginFolderMods, draft.gwPath]);

    const replaceMod = async (mod: ModDraft, next: ModDraft) => {
        update({ mods: draft.mods.map(existing => (existing === mod ? next : existing)) });
    };

    const togglePick = (mod: ModDraft) => {
        const next = new Set(picked);
        next.has(mod.fileName) ? next.delete(mod.fileName) : next.add(mod.fileName);
        setPicked(next);
    };

    const rows = [...draft.mods, ...plugins];

    return (
        <div className="pane mods flex min-w-0 flex-1 flex-col gap-2.5 overflow-auto" id="mods-pane">
            <h2 className="m-0 text-sm">Mods</h2>
            <Check
                label="Use plugin folder mods"
                checked={draft.usePluginFolderMods}
                onChange={e => update({ usePluginFolderMods: e.target.checked })}
            />
            <ul className="mods m-0 min-h-0 flex-1 list-none overflow-auto rounded border border-border p-0">
                {rows.map((mod, i) => (
                    <li
                        key={`${mod.fileName}-${i}`}
                        title={mod.fileName}
                        className={`mod-row grid grid-cols-[auto_1fr_auto] gap-2 border-b border-row-line px-2 py-1.5 ${picked.has(mod.fileName) ? 'selected' : ''}`}
                        onClick={() => {
                            if (!mod.readOnly) {
                                togglePick(mod);
                            }
                        }}
                    >
                        <input
                            type="checkbox"
                            checked={mod.active}
                            disabled={mod.readOnly}
                            onClick={e => e.stopPropagation()}
                            onChange={e => replaceMod(mod, { ...mod, active: e.target.checked })}
                        />
                        <div>
                            <div className={mod.problem ? 'problem opacity-55' : undefined}>
                                <div>{`${fileName(mod.fileName)}  [${mod.type === DLL ? 'DLL' : 'Texmod'}]`}</div>
                                {mod.readOnly && <div className="note">From plugins folder (read-only)</div>}
                                {mod.readOnly && /\.lnk$/i.test(mod.fileName) && (
                                    <div className="note">{`Shortcut to: ${mod.target ?? 'unknown'}`}</div>
                                )}
                            </div>
                            {mod.problem && <div className="note error">{mod.problem}</div>}
                        </div>
                        <div>
                            {mod.canResolve && (
                                <Button
                                    className="small"
                                    onClick={async e => {
                                        e.stopPropagation();
                                        await call('resolveMod', mod);
                                        if (!mod.readOnly) {
                                            replaceMod(mod, await call<ModDraft>('refreshMod', mod));
                                        }
                                    }}
                                >
                                    Resolve…
                                </Button>
                            )}
                        </div>
                    </li>
                ))}
            </ul>
            {message && <p className="error">{message}</p>}
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                <Button
                    onClick={async () => {
                        const path = await call<string | null>('pickFile', 'Add mod', true, draft.gwPath || null);
                        if (!path || draft.mods.some(mod => mod.fileName === path)) {
                            return;
                        }
                        const added = await call<ModDraft>('refreshMod', {
                            fileName: path,
                            type: isDll(path) ? DLL : 0,
                            active: true,
                            linuxPath: '',
                            target: null,
                            readOnly: false,
                            problem: '',
                            canLocate: false,
                            canResolve: false,
                        });
                        update({ mods: [...draft.mods, added] });
                    }}
                >
                    Add...
                </Button>
                <Button
                    onClick={() => {
                        if (picked.size === 0) {
                            setMessage('Select account mods to remove. Plugin-folder files can only be removed from the folder.');
                            return;
                        }
                        update({ mods: draft.mods.filter(mod => !picked.has(mod.fileName)) });
                        setPicked(new Set());
                        setMessage('');
                    }}
                >
                    Remove Selected
                </Button>
            </div>
        </div>
    );
}
