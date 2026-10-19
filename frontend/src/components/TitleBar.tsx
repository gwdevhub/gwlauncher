// The window's title bar. Windows are frameless, so the page draws it: dragging it moves the window, a double click maximises a
// resizable one, and its buttons minimise, maximise and close. It keeps the semantic `title-bar` class for overrides.

import type { MouseEvent, ReactNode } from 'react';
import { call } from '../bridge';

// The bar's height in CSS pixels. A window sized for a view adds it, so the view keeps its room.
export const TITLE_BAR_HEIGHT = 28;

export function TitleBar({ title, maximizable }: { title: string; maximizable: boolean }) {
    // A double click's second press maximises. The first starts a move, which ends when the button is released.
    const press = (e: MouseEvent) => {
        if (e.button !== 0) {
            return;
        }
        call(e.detail === 2 && maximizable ? 'toggleMaximize' : 'moveDrag');
    };

    return (
        <div className="title-bar flex shrink-0 items-center border-b border-border bg-surface" style={{ height: TITLE_BAR_HEIGHT }}>
            <div className="flex h-full min-w-0 flex-1 items-center px-2.5" onMouseDown={press}>
                <span className="truncate text-xs">{title}</span>
            </div>
            <TitleButton label="Minimise" onClick={() => call('minimize')}>
                <path d="M1 5.5h8" />
            </TitleButton>
            {maximizable && (
                <TitleButton label="Maximise" onClick={() => call('toggleMaximize')}>
                    <rect x="1.5" y="1.5" width="7" height="7" />
                </TitleButton>
            )}
            <TitleButton label="Close" danger onClick={() => call('close')}>
                <path d="M1.5 1.5l7 7M8.5 1.5l-7 7" />
            </TitleButton>
        </div>
    );
}

function TitleButton({ label, danger, onClick, children }: { label: string; danger?: boolean; onClick: () => void; children: ReactNode }) {
    return (
        <button
            type="button"
            aria-label={label}
            title={label}
            onClick={onClick}
            className={`title-button flex h-full w-10 cursor-default items-center justify-center border-0 bg-transparent p-0 text-[#111] ${danger ? 'hover:bg-danger hover:text-white' : 'hover:bg-surface-hover'}`}
        >
            <svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" strokeWidth="1">
                {children}
            </svg>
        </button>
    );
}
