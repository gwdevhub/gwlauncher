// The account list: the accounts, their states and issues, the empty state, and the right-click menu.

import { useEffect, useRef, useState, type MouseEvent } from 'react';
import { call } from '../bridge';
import { useChrome } from '../chrome';
import { choose, showMessage } from '../dialogs';
import { stateClass } from '../utils/format';
import { setListKeys } from '../listKeys';
import { setMenuActions } from '../menu';
import { show } from '../nav';
import { refreshState, useAppState } from '../store';
import { Button } from '../components/Button';
import { carryListState, takeCarriedListState } from '../status';
import type { AccountEntry } from '../types';
import { Footer } from '../components/Footer';

export function AccountListWindow() {
    const app = useAppState();
    const chrome = useChrome();
    const [carried] = useState(takeCarriedListState);
    const [entries, setEntries] = useState<AccountEntry[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [selected, setSelected] = useState(carried.selected ?? -1);
    const [status, setStatus] = useState(carried.status);
    const [rowIcon, setRowIcon] = useState<string | null>(null);

    // The handlers below run from events and timers, so they read the latest values through this ref.
    const latest = useRef({ entries, selected, app });
    latest.current = { entries, selected, app };

    useEffect(() => {
        let alive = true;
        (async () => {
            await refreshState();
            const icon = await call<string>('icon');
            const loaded = await call<{ entries: AccountEntry[]; error: string | null }>('accounts');
            if (!alive) {
                return;
            }
            setRowIcon(icon);
            setEntries(loaded.entries);
            setError(loaded.error ?? null);
            if (latest.current.selected >= loaded.entries.length) {
                setSelected(-1);
            }
        })();

        // States (Launching, Active, In Use) change while the list is open, so the accounts are read again every two seconds.
        const timer = setInterval(async () => {
            const { entries: next } = await call<{ entries: AccountEntry[] }>('accounts');
            if (alive && next.length === latest.current.entries.length) {
                setEntries(next);
            }
        }, 2000);

        setListKeys({
            ArrowDown: () => select(Math.min(latest.current.selected + 1, latest.current.entries.length - 1)),
            ArrowUp: () => select(Math.max(latest.current.selected - 1, 0)),
            Enter: e => latest.current.selected >= 0 && launch(latest.current.selected, e.ctrlKey),
            Delete: () => latest.current.selected >= 0 && remove(latest.current.selected),
        });

        return () => {
            alive = false;
            clearInterval(timer);
            setListKeys(null);
        };
        // Mounted once per view; the handlers read the latest values through the ref.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const { settings, setupNeeded, canLaunchDefaultClient, canCreateShortcuts } = app;

    function select(index: number) {
        setSelected(index);
        document.querySelectorAll<HTMLLIElement>('ul.accounts li')[index]?.scrollIntoView({ block: 'nearest' });
    }

    async function launch(index: number, suspended = false) {
        const name = latest.current.entries[index].name;
        setStatus(suspended ? `Starting ${name} paused…` : `Starting ${name}…`);
        const result = await call<{ issues?: unknown[]; canLaunch?: boolean; error?: string | null }>('launch', index, suspended);
        setStatus('');
        if (result.issues) {
            show('issues', { index, name, issues: result.issues, canLaunch: result.canLaunch, launchBlocked: true });
        } else if (result.error) {
            showMessage(`Can't start ${name}`, result.error);
        }
    }

    async function remove(index: number) {
        const account = latest.current.entries[index];
        if (await choose('Remove Account', `Are you sure you want to remove '${account.name}'?`, ['No', 'Yes']) !== 1) {
            return;
        }
        const failure = await call<string | null>('remove', index);
        carryListState({ status: failure ? `Could not save: ${failure}` : '', selected: -1 });
        show('list');
    }

    async function move(from: number, to: number) {
        const failure = await call<string | null>('move', from, to);
        carryListState(failure ? { status: `Could not save: ${failure}`, selected: from } : { selected: to });
        show('list');
    }

    async function refreshAccounts() {
        const failure = (await call<string | null>('reloadAccounts')) ?? '';
        carryListState({ status: failure, selected: latest.current.selected });
        show('list');
    }

    async function launchDefault() {
        const failure = (await call<string | null>('launchDefault')) ?? '';
        carryListState({ status: failure, selected: latest.current.selected });
        show('list');
    }

    async function shortcut(index: number) {
        const failure = await call<string | null>('shortcut', index);
        if (failure) {
            showMessage('Could not create the shortcut', failure);
        } else {
            setStatus(`Created a shortcut for ${latest.current.entries[index].name}.`);
        }
    }

    async function revealAccount(index: number) {
        const failure = await call<string | null>('revealAccount', index);
        if (failure) {
            showMessage('Could not open File Explorer', failure);
        }
    }

    async function updateAllClients() {
        setStatus('Updating Gw.exe…');
        setStatus((await call<string | null>('updateAll')) ?? '');
    }

    function viewIssues(index: number) {
        show('issues', { index, name: latest.current.entries[index].name, issues: null, canLaunch: false, launchBlocked: false });
    }

    // The menu's items; each action is run when the menu's window reports the pick. `hovered` is the account under the
    // pointer, or null off the rows; account items are only offered for one.
    function openMenu(e: MouseEvent, hovered: number | null) {
        e.preventDefault();
        e.stopPropagation();
        const { entries: list } = latest.current;
        const index = hovered ?? -1;
        const has = index >= 0;
        const items: { text?: string; hidden?: boolean; separator?: boolean }[] = [];
        const actions: (() => void)[] = [];
        const item = (text: string, action: () => void, shown = true) => {
            items.push({ text, hidden: !shown });
            actions.push(action);
        };
        const rule = () => {
            items.push({ separator: true });
            actions.push(() => undefined);
        };

        if (has) {
            item('Edit Account', () => show('editor', index));
            item('Move Up', () => move(index, index - 1), index > 0);
            item('Move Down', () => move(index, index + 1), index < list.length - 1);
            item('Remove Account', () => remove(index));
            item('Launch Account', () => launch(index));
            // Only offered while Ctrl is held when the menu opens.
            item('Launch Suspended...', () => launch(index, true), e.ctrlKey);
            item('Create Desktop Shortcut', () => shortcut(index), canCreateShortcuts);
            item('Open in File Explorer', () => revealAccount(index));
            item('View issues...', () => viewIssues(index));
            rule();
        }
        item('Refresh Accounts', refreshAccounts);
        item('Add New', () => show('editor', null));
        item('Launch Default GW Client', launchDefault, canLaunchDefaultClient);
        item('Update all clients', updateAllClients, list.length > 0);
        // Only offered while Ctrl is held when the menu opens, like Launch Suspended.
        item('Open DevTools', () => call('devtools'), e.ctrlKey);
        rule();
        item('Settings...', () => show('settings'));
        item('Restart', () => call('restart'));
        item('Exit', () => call('quit'));
        setMenuActions(actions);
        call('openMenu', items);
    }

    if (setupNeeded) {
        return (
            <section className="view center flex h-full flex-col justify-center gap-2.5 overflow-auto p-3.5 text-center">
                <p>Setup is needed before accounts can launch.</p>
                <Button onClick={() => show('setup')}>Open setup</Button>
            </section>
        );
    }

    const hasEntries = entries.length > 0;
    const line = error ?? status;

    return (
        <section className="view list relative flex h-full flex-col overflow-hidden" onContextMenu={e => openMenu(e, null)}>
            {settings?.keepInSystemTray && !chrome.titleBar && <div className="tray-top h-2.5 bg-divider" hidden={!hasEntries} />}
            {hasEntries && <ul
                className={`accounts m-0 min-h-0 flex-1 list-none overflow-auto p-0 ${settings?.keepInSystemTray ? 'tray' : ''}`}
                hidden={!hasEntries}
            >
                {entries.map((entry, i) => (
                    <li
                        key={entry.name + i}
                        title={entry.name}
                        className={`account-row flex cursor-default items-center justify-between gap-3 border-b border-l-2 border-divider px-2 py-1.5 hover:bg-row-hover ${entry.issues.length ? 'border-l-orange-500' : 'border-l-transparent'} ${i === selected ? 'selected' : ''}`}
                        onClick={() => select(i)}
                        onDoubleClick={e => launch(i, e.ctrlKey)}
                        onContextMenu={e => {
                            select(i);
                            openMenu(e, i);
                        }}
                    >
                        <img className="icon h-4 w-4 flex-none" src={entry.icon ?? rowIcon ?? undefined} alt="" />
                        <span className="name min-w-0 flex-1 overflow-hidden text-ellipsis whitespace-nowrap">{entry.name}</span>
                        <span className={stateClass(entry.state)}>{entry.state}</span>
                    </li>
                ))}
            </ul>}
            {!hasEntries && (
                // Centred over the whole window, not the space above the status line and footer.
                <div className="empty pointer-events-none flex-1 flex items-center justify-center">
                    <div className="stack pointer-events-auto flex flex-col items-center gap-2">
                        <Button onClick={() => show('editor', null)}>Add Account</Button>
                        {canLaunchDefaultClient && <Button onClick={() => launchDefault()}>Launch Default GW Client</Button>}
                    </div>
                </div>
            )}
            <p id="status" className="status min-h-[1.4em] px-1.5 py-1 text-xs text-muted">{line}</p>
            <Footer version={app.version} update={app.update} />
        </section>
    );
}
