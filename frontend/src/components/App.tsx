// The page's root: which view shows, the host's events, and the keyboard. Each view is one component in its own file.

import { useEffect, useState } from 'react';
import { call } from '../bridge';
import { loadChrome } from '../chrome';
import { on } from '../events';
import { listKey } from '../listKeys';
import { runMenuAction } from '../menu';
import { bindViewSetter, openView, session, show, showNow, type ViewState } from '../nav';
import { refreshState, setSettings, useAppState } from '../store';
import { applyUiScale } from '../scale';
import { showMessage } from '../dialogs';
import { AccountEditWindow } from '../windows/AccountEditWindow';
import { IssuesWindow, type IssuesRequest } from '../windows/IssuesWindow';
import { AccountListWindow } from '../windows/AccountListWindow';
import { PasswordWindow } from '../windows/PasswordWindow';
import { SettingsWindow } from '../windows/SettingsWindow';
import { SetupWindow } from '../windows/SetupWindow';
import { ContextMenuWindow } from '../windows/ContextMenuWindow';
import { ChoiceWindow } from '../windows/ChoiceWindow';
import { MessageWindow } from '../windows/MessageWindow';
import { ProgressWindow } from '../windows/ProgressWindow';
import { UpdateWindow } from '../windows/UpdateWindow';
import type { MenuItem, Role, Settings } from '../types';
import { WindowFrame } from './WindowFrame';

export function App() {
    const [view, setView] = useState<ViewState | null>(null);
    const app = useAppState();

    useEffect(() => applyUiScale(app.settings?.uiScale ?? 100), [app.settings?.uiScale]);

    // Bound first, so the start-up view below can be shown.
    useEffect(() => bindViewSetter(setView), []);

    useEffect(() => {
        (async () => {
            // Before the first view, so a popup's size counts the title bar.
            await loadChrome();
            const state = await refreshState();
            const role: Role | null = await call<Role | null>('window');
            session.popup = role;
            if (role?.view === 'menu') {
                showNow('menu', [role.args[0]]);
            } else if (role) {
                show(role.view, ...role.args);
            } else if (state.locked) {
                show('password');
            } else if (state.startup) {
                openView(state.startup, showMessage);
            } else {
                show('list');
            }
        })();
    }, []);

    // The host pushes these: a view to open from the tray, a refresh of the list, and a pick from the tray menu.
    useEffect(() => {
        const offs = [
            on('navigate', (v: Parameters<typeof openView>[0]) => openView(v, showMessage)),
            on('refresh', () => {
                if (!session.popup && session.current === 'list') {
                    show('list');
                }
            }),
            on('menuPick', (index: number) => runMenuAction(index)),
            on('settings', (settings: Settings) => setSettings(settings)),
            on('uiScale', (percent: number) => applyUiScale(percent)),
        ];
        return () => offs.forEach(off => off());
    }, []);

    // Escape closes the list or the menu, and returns to the list from anywhere else. Arrow, Enter and Delete act on the list.
    useEffect(() => {
        const onKey = (e: KeyboardEvent) => {
            if (e.key === 'Escape') {
                if (session.popup?.view === 'menu' || session.current === 'list') {
                    call('hide');
                } else {
                    show('list');
                }
                return;
            }
            const handler = session.current === 'list' ? listKey(e.key) : null;
            if (handler) {
                e.preventDefault();
                handler(e);
            }
        };
        const noMenu = (e: Event) => e.preventDefault();
        document.addEventListener('keydown', onKey);
        document.addEventListener('contextmenu', noMenu);
        return () => {
            document.removeEventListener('keydown', onKey);
            document.removeEventListener('contextmenu', noMenu);
        };
    }, []);

    return <WindowFrame>{view && <Current key={view.key} name={view.name} args={view.args} />}</WindowFrame>;
}

// Picks the view component for a view name, with the arguments the view was shown with.
function Current({ name, args }: { name: string; args: any[] }) {
    switch (name) {
        case 'editor':
            return <AccountEditWindow index={args[0] ?? null} keepDraft={args[1]} copyOf={args[2] ?? null} />;
        case 'issues':
            return <IssuesWindow view={args[0] as IssuesRequest} />;
        case 'settings':
            return <SettingsWindow />;
        case 'setup':
            return <SetupWindow />;
        case 'password':
            return <PasswordWindow />;
        case 'message':
            return <MessageWindow title={args[0]?.title ?? ''} text={args[0]?.text ?? ''} />;
        case 'choose':
            return <ChoiceWindow {...args[0]} />;
        case 'update':
            return <UpdateWindow {...args[0]} />;
        case 'progress':
            return <ProgressWindow {...args[0]} />;
        case 'menu':
            return <ContextMenuWindow items={(args[0] ?? []) as MenuItem[]} />;
        default:
            return <AccountListWindow />;
    }
}
