'use strict';

// GW Launcher's window. The same page runs in WebView2 on Windows and WebKitGTK on Linux. call() sends a request to the
// C# host and returns a promise for its reply; the host's replies and events arrive through window.gwlReceive.

// ===========================================================================
// Bridge
// ===========================================================================

const pending = new Map();
let nextRequest = 1;

function post(text) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(text);
    } else {
        window.webkit.messageHandlers.gwl.postMessage(text);
    }
}

function call(method, ...args) {
    const id = nextRequest++;
    return new Promise((resolve, reject) => {
        pending.set(id, { resolve, reject });
        post(JSON.stringify({ id, method, args }));
    });
}

window.gwlReceive = message => {
    if (message.event) {
        events[message.event]?.(message.data);
        return;
    }

    const request = pending.get(message.id);
    pending.delete(message.id);
    if (message.error !== undefined) {
        request?.reject(new Error(message.error));
    } else {
        request?.resolve(message.result);
    }
};

// Pushed by the host: opening a view from the tray, and the prompts and progress it starts itself.
const events = {
    navigate: view => openView(view),
    message: m => showMessage(m.title, m.text),
    choose: c => choose(c.title, c.message, c.options).then(answer => call('answer', c.id, answer)),
    progress: p => progressEvent(p)
};

// ===========================================================================
// Helpers
// ===========================================================================

function h(tag, props = {}, ...children) {
    const element = document.createElement(tag);
    for (const [key, value] of Object.entries(props)) {
        if (key.startsWith('on')) {
            element.addEventListener(key.slice(2), value);
        } else if (key === 'class') {
            element.className = value;
        } else if (key === 'value' || key === 'checked' || key === 'disabled') {
            element[key] = value;
        } else if (value !== false && value != null) {
            element.setAttribute(key, value === true ? '' : value);
        }
    }

    for (const child of children.flat()) {
        if (child != null && child !== false) {
            element.append(child instanceof Node ? child : String(child));
        }
    }

    return element;
}

const button = (text, onclick, props = {}) => h('button', { onclick, ...props }, text);
const fileName = path => path.split(/[\\/]/).pop();
const isDll = path => /\.dll$/i.test(path);
const DLL = 1;

// ===========================================================================
// Navigation
// ===========================================================================

// Each view's window size. Forms are pinned, so the window doesn't hide when focus moves to a file picker.
const sizes = {
    list: [220, 340], editor: [380, 470], mods: [470, 460], issues: [470, 380],
    settings: [420, 500], password: [380, 240], setup: [460, 420]
};

let state = {};
let accounts = [];
let selected = -1;
let status = '';
let draft = null;
let editing = null;
let poll = null;

async function show(name, ...args) {
    clearInterval(poll);
    closeMenu();
    call('pin', name !== 'list');
    call('resize', ...sizes[name]);
    document.getElementById('view').replaceChildren(await views[name](...args));
}

// Opens a view the host asked for: from the tray menu, or after a desktop shortcut couldn't launch.
function openView(view) {
    if (view.view === 'issues') {
        show('issues', view);
    } else if (view.view === 'message') {
        show('list').then(() => showMessage(view.title, view.text));
    } else {
        show(view.view);
    }
}

async function start() {
    state = await call('state');
    if (state.locked) {
        show('password');
    } else if (state.startup) {
        openView(state.startup);
    } else {
        show('list');
    }
}

document.addEventListener('contextmenu', e => e.preventDefault());
document.addEventListener('click', closeMenu);
document.addEventListener('keydown', e => {
    if (e.key !== 'Escape') {
        return;
    }

    if (!document.getElementById('overlay').hidden) {
        return;
    }

    if (views.current !== 'list') {
        show('list');
    } else {
        call('hide');
    }
});

const views = {};

// ===========================================================================
// Account list
// ===========================================================================

views.list = async () => {
    views.current = 'list';
    state = await call('state');
    if (state.setupNeeded) {
        return h('section', { class: 'center' },
            h('p', {}, 'Setup is needed before accounts can launch.'),
            button('Open setup', () => show('setup')));
    }

    const { entries, error } = await call('accounts');
    accounts = entries;
    if (selected >= accounts.length) {
        selected = -1;
    }

    const list = h('ul', { class: 'accounts', oncontextmenu: e => openMenu(e) },
        entries.map((entry, i) => h('li', {
            class: i === selected ? 'selected' : '',
            onclick: () => select(i),
            ondblclick: () => launch(i),
            oncontextmenu: () => select(i)
        }, entry.blocked ? '⚠ ' : '', entry.name)));

    const line = error ?? (status || (entries.length === 0 ? 'No accounts yet. Right-click to add one.' : ''));
    status = '';
    return h('section', { class: 'list' }, list, h('p', { id: 'status', class: 'status' }, line));
};

function select(index) {
    selected = index;
    document.querySelectorAll('ul.accounts li').forEach((li, i) => li.classList.toggle('selected', i === index));
}

function setStatus(text) {
    const line = document.getElementById('status');
    if (line) {
        line.textContent = text;
    }
}

function openMenu(e) {
    e.preventDefault();
    e.stopPropagation();
    const has = selected >= 0;
    const item = (text, action, enabled = true) =>
        h('div', { class: enabled ? '' : 'disabled', onclick: () => { closeMenu(); if (enabled) action(); } }, text);

    const menu = document.getElementById('menu');
    menu.replaceChildren(
        item('Refresh Accounts', () => show('list')),
        h('hr'),
        item('Add New', () => show('editor', null)),
        item('Edit Selected', () => show('editor', selected), has),
        item('Move Up', () => move(selected, selected - 1), has && selected > 0),
        item('Move Down', () => move(selected, selected + 1), has && selected < accounts.length - 1),
        item('Remove Selected', () => remove(selected), has),
        item('Launch Selected', () => launch(selected), has),
        item('Create Desktop Shortcut', () => shortcut(selected), has && state.canCreateShortcuts),
        item('View issues...', () => viewIssues(selected), has),
        h('hr'),
        item('Exit', () => call('quit')));

    menu.hidden = false;
    menu.style.left = `${Math.min(e.clientX, innerWidth - menu.offsetWidth - 4)}px`;
    menu.style.top = `${Math.min(e.clientY, innerHeight - menu.offsetHeight - 4)}px`;
}

function closeMenu() {
    document.getElementById('menu').hidden = true;
}

async function move(from, to) {
    const error = await call('move', from, to);
    if (error) {
        status = `Could not save: ${error}`;
    } else {
        selected = to;
    }

    show('list');
}

async function remove(index) {
    if (await choose('Remove Account', `Are you sure you want to remove '${accounts[index].name}'?`, ['No', 'Yes']) !== 1) {
        return;
    }

    const error = await call('remove', index);
    status = error ? `Could not save: ${error}` : '';
    selected = -1;
    show('list');
}

async function launch(index) {
    const name = accounts[index].name;
    setStatus(`Starting ${name}…`);
    const result = await call('launch', index);
    setStatus('');
    if (result.issues) {
        show('issues', { index, name, issues: result.issues, canLaunch: result.canLaunch, launchBlocked: true });
    } else if (result.error) {
        showMessage(`Can't start ${name}`, result.error);
    }
}

async function shortcut(index) {
    const error = await call('shortcut', index);
    if (error) {
        showMessage('Could not create the shortcut', error);
    } else {
        setStatus(`Created a shortcut for ${accounts[index].name}.`);
    }
}

async function viewIssues(index) {
    const issues = await call('issues', index);
    show('issues', { index, name: accounts[index].name, issues, canLaunch: false, launchBlocked: false });
}

// ===========================================================================
// Account editor
// ===========================================================================

// Field rows edit the draft directly; Save sends it to the host, which validates it.
views.editor = async (index, keepDraft) => {
    views.current = 'editor';
    if (!keepDraft) {
        draft = await call('draft', index);
        editing = index;
    }

    const field = (label, key, attrs = {}, trailing = null) => h('label', { class: 'row' },
        h('span', {}, label),
        h('input', { type: 'text', value: draft[key] ?? '', ...attrs, oninput: e => { draft[key] = e.target.value; } }),
        trailing ?? h('span'));
    const browse = key => button('...', async () => {
        const path = await call('pickFile', 'Select Gw.exe', false);
        if (path) {
            draft[key] = path;
            show('editor', editing, true);
        }
    }, { class: 'small' });

    const credentials = [
        field('Email:', 'email'),
        field('Pass:', 'password', { type: 'password' }),
        field('Char:', 'character')
    ];
    const setCredentialsVisible = () => credentials.forEach(row => { row.hidden = draft.steamAccount; });
    const error = h('p', { class: 'error' });
    const broken = draft.mods.some(mod => mod.active && !mod.readOnly && mod.problem);

    const body = h('section', {},
        h('h2', {}, editing == null ? 'Add Account' : 'Modify Account'),
        field('Title:', 'title'),
        credentials,
        field(state.hasLinuxPaths ? 'Win path:' : 'Path:', 'gwPath', {}, state.hasLinuxPaths ? null : browse('gwPath')),
        state.hasLinuxPaths && field('Linux path:', 'linuxGwPath', {}, browse('linuxGwPath')),
        field('Ex Args:', 'extraArgs'),
        state.hasWindowsOptions && h('label', { class: 'check' },
            h('input', { type: 'checkbox', checked: draft.steamAccount, onchange: e => { draft.steamAccount = e.target.checked; setCredentialsVisible(); } }),
            'Steam account'),
        state.hasWindowsOptions && h('label', { class: 'check' },
            h('input', { type: 'checkbox', checked: draft.elevated, onchange: e => { draft.elevated = e.target.checked; } }),
            'Run as administrator'),
        error,
        h('div', { class: 'buttons' },
            button(`Mods (${draft.mods.length})${broken ? ' ⚠' : ''}`, () => show('mods'), { class: 'left' }),
            button('Cancel', () => show('list')),
            button('Save', async () => {
                const message = await call('save', editing, draft);
                if (message) {
                    error.textContent = message;
                    return;
                }

                selected = editing ?? accounts.length;
                show('list');
            })));
    setCredentialsVisible();
    return body;
};

// ===========================================================================
// Mod manager
// ===========================================================================

// Edits the draft's mods. Plugin-folder files are listed read-only when the account uses them.
views.mods = async () => {
    views.current = 'mods';
    const plugins = draft.usePluginFolderMods ? await call('pluginFiles', draft) : [];
    const message = h('p', { class: 'error' });
    const rows = [...draft.mods, ...plugins];
    const picked = new Set();

    const row = mod => {
        const details = h('div', { class: mod.problem ? 'problem' : '' },
            h('div', {}, `${fileName(mod.fileName)}  [${mod.type === DLL ? 'DLL' : 'Texmod'}]`),
            mod.canLocate && h('div', { class: 'note' }, `Linux: ${mod.linuxPath || 'not set'}`),
            mod.readOnly && h('div', { class: 'note' }, 'From plugins folder (read-only)'),
            mod.readOnly && /\.lnk$/i.test(mod.fileName) && h('div', { class: 'note' }, `Shortcut to: ${mod.target ?? 'unknown'}`));
        const problem = mod.problem && h('div', { class: 'note error' }, mod.problem);
        const actions = h('div', {},
            mod.canLocate && button('Locate...', async e => {
                e.stopPropagation();
                const path = await call('pickFile', `Locate ${fileName(mod.fileName)}`, true);
                if (path) {
                    mod.linuxPath = path;
                    Object.assign(mod, await call('refreshMod', mod));
                    show('mods');
                }
            }, { class: 'small' }),
            mod.canResolve && button('Resolve…', async e => {
                e.stopPropagation();
                await call('resolveMod', mod);
                if (!mod.readOnly) {
                    Object.assign(mod, await call('refreshMod', mod));
                }
                show('mods');
            }, { class: 'small' }));

        const item = h('li', {
            title: mod.fileName,
            onclick: () => {
                if (mod.readOnly) {
                    return;
                }
                picked.has(mod) ? picked.delete(mod) : picked.add(mod);
                item.classList.toggle('selected', picked.has(mod));
            }
        },
            h('input', { type: 'checkbox', checked: mod.active, disabled: mod.readOnly, onclick: e => e.stopPropagation(), onchange: e => { mod.active = e.target.checked; } }),
            h('div', {}, details, problem),
            actions);
        return item;
    };

    return h('section', {},
        h('h2', {}, 'Mod Manager'),
        h('label', { class: 'check' },
            h('input', { type: 'checkbox', checked: draft.usePluginFolderMods, onchange: e => { draft.usePluginFolderMods = e.target.checked; show('mods'); } }),
            'Use plugin folder mods'),
        h('ul', { class: 'mods' }, rows.map(row)),
        message,
        h('div', { class: 'buttons' },
            button('Add...', async () => {
                const path = await call('pickFile', 'Add mod', true);
                if (!path || draft.mods.some(mod => mod.fileName === path)) {
                    return;
                }

                draft.mods.push(await call('refreshMod', {
                    fileName: path, type: isDll(path) ? DLL : 0, active: true, linuxPath: '',
                    target: null, readOnly: false, problem: '', canLocate: false, canResolve: false
                }));
                show('mods');
            }),
            button('Remove Selected', () => {
                if (picked.size === 0) {
                    message.textContent = 'Select account mods to remove. Plugin-folder files can only be removed from the folder.';
                    return;
                }

                draft.mods = draft.mods.filter(mod => !picked.has(mod));
                show('mods');
            }),
            button('Back', () => show('editor', editing, true))));
};

// ===========================================================================
// Issues
// ===========================================================================

// Blocking issues first. Resolve runs the fix the issue offers; Launch unlocks once no blocking issues are left.
views.issues = async ({ index, name, issues, canLaunch, launchBlocked }) => {
    views.current = 'issues';
    const line = h('p', { class: 'note' });
    const unresolved = issues.filter(issue => issue.severity === 0);
    const launchButton = canLaunch && button('Launch', async () => {
        const error = await call('launchNow', index);
        if (error) {
            line.textContent = `Can't start ${name}: ${error}`;
        } else {
            show('list');
        }
    }, { disabled: unresolved.length > 0 });

    const row = issue => {
        const blocking = issue.severity === 0;
        const fixable = issue.wofPath || issue.canDownloadGMod || issue.canUpdateGwExe;
        const item = h('div', {},
            h('p', { class: blocking ? 'error' : 'warning' }, (blocking ? 'Blocking: ' : 'Warning: ') + issue.message));
        if (fixable) {
            const resolve = button('Resolve…', async () => {
                resolve.disabled = true;
                const message = await call('fix', index, issue);
                line.textContent = message ?? 'Nothing was changed.';
                resolve.disabled = message == null;
                if (message != null) {
                    unresolved.splice(unresolved.indexOf(issue), 1);
                    if (launchButton) {
                        launchButton.disabled = unresolved.length > 0;
                    }
                    if (issue.canUpdateGwExe) {
                        item.hidden = true;
                    }
                }
            }, { class: 'small' });
            item.append(resolve);
        }

        return item;
    };

    const sorted = [...issues].sort((a, b) => a.severity - b.severity);
    return h('section', {},
        h('h2', {}, launchBlocked ? `Can't launch ${name}` : `Issues: ${name}`),
        h('p', {}, launchBlocked ? 'Fix these before launching:' : 'Issues found for this account:'),
        issues.length === 0 ? h('p', {}, 'No issues. This account should launch.') : sorted.map(row),
        line,
        h('div', { class: 'buttons' }, launchButton, button('Close', () => show('list'))));
};

// ===========================================================================
// Settings, password and setup
// ===========================================================================

views.settings = async () => {
    views.current = 'settings';
    state = await call('state');
    const settings = { ...state.settings };
    const error = h('p', { class: 'error' });
    const box = (key, label, description, enabled = true) => h('div', {},
        h('label', { class: 'check' },
            h('input', { type: 'checkbox', id: key, checked: settings[key], disabled: !enabled, onchange: e => { settings[key] = e.target.checked; refresh(); } }),
            label),
        h('p', { class: 'note indent' }, description));
    const refresh = () => {
        document.getElementById('autoUpdate').disabled = !settings.checkForUpdates;
        document.getElementById('launchMinimized').disabled = !(state.hasTray && settings.keepInSystemTray);
    };

    const body = h('section', {},
        h('h2', {}, 'General'),
        box('keepInSystemTray', 'Keep in system tray', state.hasTray
            ? 'Run from a tray icon; closing hides it to the tray.'
            : 'Not available: no system tray was found on this desktop.', state.hasTray),
        box('launchMinimized', 'Launch minimised', 'Start hidden in the tray instead of showing the window.'),
        h('h2', {}, 'Updates'),
        box('checkForUpdates', 'Check for updates', 'Look for new launcher and Gw.exe versions at startup.'),
        box('autoUpdate', 'Auto update', 'Install launcher updates without asking first.'),
        h('h2', {}, 'Advanced'),
        h('label', { class: 'row wide' }, h('span', {}, 'Timeout (ms):'),
            h('input', { type: 'text', value: String(settings.timeoutOnModlaunch), oninput: e => { settings.timeoutOnModlaunch = e.target.value; } }),
            h('span')),
        h('p', { class: 'note' }, 'How long to wait for a mod DLL to load into the game.'),
        error,
        h('div', { class: 'buttons' },
            button('Cancel', () => show('list')),
            button('OK', async () => {
                const timeout = Number(settings.timeoutOnModlaunch);
                if (!Number.isInteger(timeout) || timeout <= 0) {
                    error.textContent = 'Enter the mod launch timeout as a whole number of milliseconds.';
                    return;
                }

                settings.timeoutOnModlaunch = timeout;
                await call('saveSettings', settings);
                show('list');
            })));
    setTimeout(refresh);
    return body;
};

views.password = async () => {
    views.current = 'password';
    const password = h('input', { type: 'password', placeholder: 'Master password' });
    const remember = h('input', { type: 'checkbox' });
    const error = h('p', { class: 'error' });
    const submit = async () => {
        if (!password.value) {
            error.textContent = 'Enter the master password.';
            return;
        }

        const message = await call('unlock', password.value, remember.checked);
        if (message) {
            error.textContent = message;
        } else {
            show('list');
        }
    };

    password.addEventListener('keydown', e => { if (e.key === 'Enter') submit(); });
    setTimeout(() => password.focus());
    return h('section', {},
        h('p', {}, 'Accounts are encrypted. Enter the master password to open them.'),
        password,
        state.hasWindowsOptions && h('label', { class: 'check' }, remember, "Don't ask again on this computer"),
        error,
        h('div', { class: 'buttons' }, button('Quit', () => call('quit')), button('OK', submit)));
};

// Prerequisites the platform installs, one at a time. A running install keeps going if the window closes.
views.setup = async () => {
    views.current = 'setup';
    const [items, progress] = await Promise.all([call('setup'), call('setupStatus')]);
    const next = items.findIndex(item => !item.done);
    const complete = next < 0;
    const steps = h('ul', { class: 'steps' }, items.map((item, i) => h('li', { class: item.done ? 'done' : i === next ? '' : 'later' },
        h('span', { class: 'mark' }, item.done ? '✓' : '○'),
        h('div', {}, h('strong', {}, item.title), h('p', { class: 'note' }, item.description)))));

    let message;
    if (complete) {
        message = 'Click the tray icon to open the launcher.';
    } else if (progress.running) {
        message = 'Installing. This can take several minutes. You can close this window; the install keeps going.';
    } else if (!items[next].canInstall) {
        message = `${items[next].title} can't be installed automatically on this distro. Install it with your package manager, then reopen this window.`;
    } else {
        message = 'Click install. If asked, enter your password in the system prompt.';
    }

    if (progress.running) {
        poll = setInterval(async () => {
            const now = await call('setupStatus');
            if (!now.running) {
                show('setup');
            } else {
                document.getElementById('setupLog').textContent = now.lastLine;
            }
        }, 500);
    }

    return h('section', {},
        h('h2', {}, complete ? 'Setup complete' : 'GW Launcher needs a few things before it can run accounts.'),
        steps,
        h('p', {}, message),
        progress.running && h('progress'),
        progress.running && h('p', { class: 'note', id: 'setupLog' }, progress.lastLine),
        h('div', { class: 'buttons' },
            !complete && button(`Install ${items[next].title}`, async () => {
                const error = await call('startSetup');
                if (error) {
                    showMessage('Setup', error);
                }
                show('setup');
            }, { disabled: progress.running || !items[next].canInstall }),
            button('Close', () => show('list'))));
};

// ===========================================================================
// Overlays: message, choice and progress
// ===========================================================================

function overlay(...children) {
    const panel = document.getElementById('overlay');
    panel.replaceChildren(h('div', {}, ...children));
    panel.hidden = false;
    return () => { panel.hidden = true; };
}

// An error message with the versions, and a button that copies it all for a bug report.
function showMessage(title, text) {
    const report = `${title}\n${text}\n\n${state.version ?? ''}`;
    const close = overlay(
        h('h2', {}, title),
        h('p', { class: 'error' }, text),
        h('p', { class: 'note' }, state.version ?? ''),
        h('div', { class: 'buttons' },
            button('Copy', e => { call('copy', report); e.target.textContent = 'Copied'; }),
            button('OK', () => close())));
}

function choose(title, message, options) {
    return new Promise(resolve => {
        const close = overlay(
            h('h2', {}, title),
            h('p', {}, message),
            h('div', { class: 'buttons' }, options.map((option, i) => button(option, () => { close(); resolve(i); }))));
    });
}

let progressView = null;

function progressEvent(p) {
    if (p.phase === 'start') {
        const bar = h('progress');
        const detail = h('p', { class: 'note' });
        const close = overlay(
            h('h2', {}, p.heading),
            p.note && h('p', { class: 'note' }, p.note),
            bar,
            detail,
            p.cancellable && h('div', { class: 'buttons' }, button('Cancel', e => { e.target.disabled = true; e.target.textContent = 'Cancelling…'; call('cancel'); })));
        progressView = { bar, detail, close };
    } else if (p.phase === 'update' && progressView) {
        if (p.fraction == null) {
            progressView.bar.removeAttribute('value');
        } else {
            progressView.bar.value = p.fraction;
        }
        progressView.detail.textContent = p.detail;
    } else if (p.phase === 'end') {
        progressView?.close();
        progressView = null;
    } else if (p.phase === 'failed') {
        progressView = null;
        const close = overlay(h('p', { class: 'error' }, p.text), h('div', { class: 'buttons' }, button('Close', () => close())));
    }
}

start();
