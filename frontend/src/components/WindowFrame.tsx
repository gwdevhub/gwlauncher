// The frame every view sits in: the title bar, a border, and the edges that resize the window. Windows are frameless, so the
// page draws what the window manager would. It keeps the semantic `window-frame` class for overrides.

import type { ReactNode } from 'react';
import { call } from '../bridge';
import { getChrome, useChrome } from '../chrome';
import type { Chrome } from '../types';
import { TITLE_BAR_HEIGHT, TitleBar } from './TitleBar';

// The zones along each edge and corner: the ResizeEdge index the host takes, the zone's place, and its cursor.
const RESIZE_ZONES: [number, string, string][] = [
    [0, 'left-0 top-0 h-2 w-2', 'cursor-nwse-resize'],
    [1, 'left-2 right-2 top-0 h-1', 'cursor-ns-resize'],
    [2, 'right-0 top-0 h-2 w-2', 'cursor-nesw-resize'],
    [3, 'bottom-2 left-0 top-2 w-1', 'cursor-ew-resize'],
    [4, 'bottom-2 right-0 top-2 w-1', 'cursor-ew-resize'],
    [5, 'bottom-0 left-0 h-2 w-2', 'cursor-nesw-resize'],
    [6, 'bottom-0 left-2 right-2 h-1', 'cursor-ns-resize'],
    [7, 'bottom-0 right-0 h-2 w-2', 'cursor-nwse-resize'],
];

// A window with a title bar or resize edges is bordered, so it stands out from what's behind it. A menu or progress popup draws its own.
function bordered(chrome: Chrome) {
    return chrome.titleBar || chrome.resizable;
}

// The window size that gives a view the size it asks for, with the title bar and border around it.
export function withFrame(size: [number, number]): [number, number] {
    const chrome = getChrome();
    if (!size || !bordered(chrome)) {
        return size;
    }
    return [size[0] + 2, size[1] + 2 + (chrome.titleBar ? TITLE_BAR_HEIGHT : 0)];
}

export function WindowFrame({ children }: { children: ReactNode }) {
    const chrome = useChrome();
    return (
        <div className={`window-frame relative flex h-full flex-col ${bordered(chrome) ? 'border border-border' : ''}`}>
            {chrome.titleBar && <TitleBar title={chrome.title} maximizable={chrome.resizable} />}
            <div className="window-content flex min-h-0 flex-1 flex-col">{children}</div>
            {chrome.resizable &&
                RESIZE_ZONES.map(([edge, place, cursor]) => (
                    <div key={edge} className={`absolute z-10 ${place} ${cursor}`} onMouseDown={e => e.button === 0 && call('resizeDrag', edge)} />
                ))}
        </div>
    );
}
