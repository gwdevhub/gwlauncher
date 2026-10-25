// Settings: the general options, the master password, updates and launch timeouts.

import { useEffect, useState } from 'react';
import { call } from '../bridge';
import { choose } from '../dialogs';
import { show } from '../nav';
import { refreshState, setSettings, useAppState } from '../store';
import { useAutosize } from '../autosize';
import type { Settings } from '../types';
import { Button } from '../components/Button';
import { Check } from '../components/Check';
import { Note } from '../components/Note';
import { parseTimeoutSeconds, timeoutSecondsText } from '../utils/numbers';

export function SettingsWindow() {
    const app = useAppState();
    const [loaded, setLoaded] = useState(false);
    const [encrypted, setEncrypted] = useState(false);
    const [settings, setLocal] = useState<Settings | null>(null);
    // The window fits the page, so the root is sized by its content, not the window's width.
    const [root, setRoot] = useState<HTMLElement | null>(null);
    useAutosize(root);
    const [protect, setProtect] = useState(false);
    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const [error, setError] = useState('');
    const [launchTimeoutSeconds, setLaunchTimeoutSeconds] = useState('');

    useEffect(() => {
        (async () => {
            const state = await refreshState();
            const isEncrypted = await call<boolean>('encrypted');
            setEncrypted(isEncrypted);
            setProtect(isEncrypted);
            setLocal({ ...state.settings! });
            setLaunchTimeoutSeconds(timeoutSecondsText(state.settings!.timeoutOnModlaunch));
            setLoaded(true);
        })();
    }, []);

    if (!loaded || !settings) {
        return <section ref={setRoot} className="view flex w-max self-start flex-col gap-2.5 p-3.5" />;
    }

    const set = (patch: Partial<Settings>) => setLocal({ ...settings, ...patch });
    const trayOptionsDisabled = !(app.hasTray && settings.keepInSystemTray);

    // Applies the password change. False when it failed or was cancelled, and the settings stay as they were.
    const applyPassword = async (): Promise<boolean> => {
        if (protect && !password && !encrypted) {
            setError('Enter a password, or untick "Password protect Accounts.json".');
            return false;
        }
        if (!(protect ? password !== '' : encrypted)) {
            return true;
        }
        if (!protect && await choose('Remove password protection', 'Accounts.json will be stored as plain text, including your account passwords.', ['No', 'Yes']) !== 1) {
            return false;
        }
        const message = await call<string | null>('setMasterPassword', protect ? password : '');
        if (message) {
            setError(message);
            return false;
        }
        return true;
    };

    const save = async () => {
        const timeout = parseTimeoutSeconds(launchTimeoutSeconds);
        if (timeout === undefined) {
            setError('Enter the launch timeout in whole seconds, from 1 to 4294967.');
            return;
        }
        const saved = { ...settings, timeoutOnModlaunch: timeout, steamPath: settings.steamPath.trim(), winePath: settings.winePath.trim() };
        const message = await call<string | null>('saveSettings', saved);
        if (message) {
            setError(message);
            return;
        }
        if (!await applyPassword()) {
            return;
        }
        setSettings(saved);
        show('list');
    };

    return (
        <section ref={setRoot} className="view flex w-max self-start flex-col gap-2.5 p-3.5">
            <h2 className="m-0 text-sm">General</h2>
            <Check
                label="Keep in system tray"
                checked={settings.keepInSystemTray}
                disabled={!app.hasTray}
                onChange={e => set({ keepInSystemTray: e.target.checked })}
            />
            <Note className="indent ml-[22px]">
                {app.hasTray ? 'Closing hides the window unless the title bar is shown.' : 'No system tray available.'}
            </Note>
            <Check
                label="Start hidden in tray"
                checked={settings.launchMinimized}
                disabled={trayOptionsDisabled}
                onChange={e => set({ launchMinimized: e.target.checked })}
            />
            <Check
                label="Show title bar in tray mode"
                checked={settings.showTitleBarInTray}
                disabled={trayOptionsDisabled}
                onChange={e => set({ showTitleBarInTray: e.target.checked })}
            />

            <h2 className="m-0 text-sm">Display</h2>
            <label className="row form-grid-wide grid items-center gap-2">
                <span>UI scale:</span>
                <input className="w-full" type="range" min={50} max={200} step={5} value={settings.uiScale} onChange={e => set({ uiScale: e.target.valueAsNumber })} />
                <span>{settings.uiScale}%</span>
            </label>

            <h2 className="m-0 text-sm">Security</h2>
            <Check label="Password protect Accounts.json" checked={protect} onChange={e => setProtect(e.target.checked)} />
            <div hidden={!protect} className="flex flex-col gap-2.5">
                <label className="row form-grid-wide grid items-center gap-2">
                    <span>Master password:</span>
                    <input className="text-input w-full select-text rounded border border-border px-1.5 py-1" type={showPassword ? 'text' : 'password'} value={password} placeholder={encrypted ? '(set)' : '(none)'} onChange={e => setPassword(e.target.value)} />
                    <span />
                </label>
                <Check className="indent ml-[22px]" label="Show password" checked={showPassword} onChange={e => setShowPassword(e.target.checked)} />
            </div>

            <h2 className="m-0 text-sm">Updates</h2>
            <Check
                label="Prompt for updates at startup"
                checked={settings.promptForUpdates}
                onChange={e => set({ promptForUpdates: e.target.checked })}
            />
            <Note className="indent ml-[22px]">
                The update icon is shown even when prompts are disabled.
            </Note>

            {app.hasLinuxPaths && (
                <>
                    <h2 className="m-0 text-sm">Locations</h2>
                    <label className="row form-grid-wide grid items-center gap-2">
                        <span>Steam folder:</span>
                        <input className="text-input w-full select-text rounded border border-border px-1.5 py-1" type="text" value={settings.steamPath} placeholder={app.detectedPaths?.steam || 'Not found'} onChange={e => set({ steamPath: e.target.value })} />
                        <span />
                    </label>
                    <label className="row form-grid-wide grid items-center gap-2">
                        <span>Wine program:</span>
                        <input className="text-input w-full select-text rounded border border-border px-1.5 py-1" type="text" value={settings.winePath} placeholder={app.detectedPaths?.wine || 'Not found'} onChange={e => set({ winePath: e.target.value })} />
                        <span />
                    </label>
                    <Note>Leave blank to auto-detect.</Note>
                </>
            )}

            <h2 className="m-0 text-sm">Advanced</h2>
            <label className="row grid grid-cols-[220px_1fr_auto] items-center gap-2">
                <span>Launch timeout (seconds):</span>
                <input className="text-input w-full select-text rounded border border-border px-1.5 py-1" type="number" min="1" max="4294967" step="1" value={launchTimeoutSeconds} onChange={e => setLaunchTimeoutSeconds(e.target.value)} />
                <span />
            </label>
            <Note>Per-step timeout for mod loading, the game window, and input readiness.</Note>

            {error && <p className="error text-danger">{error}</p>}
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                <Button onClick={() => show('list')}>Cancel</Button>
                <Button onClick={save}>OK</Button>
            </div>
        </section>
    );
}
