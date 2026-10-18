// The account list's context menu, in its own window. A pick is reported to the host, which runs it in the list.

import { useState } from 'react';
import { call } from '../bridge';
import { useAutosize } from '../autosize';
import type { MenuItem } from '../types';

export function ContextMenuWindow({ items }: { items: MenuItem[] }) {
    // The window fits the menu as it's drawn.
    const [menu, setMenu] = useState<HTMLDivElement | null>(null);
    useAutosize(menu);

    return (
        <div className="menu w-max min-w-[190px] self-start border border-border bg-white py-1" ref={setMenu}>
            {items.map((entry, i) =>
                entry.separator ? (
                    <hr key={i} className="my-1 border-0 border-t border-row-line" />
                ) : (
                    <div
                        key={i}
                        className="menu-item cursor-default whitespace-nowrap px-3.5 py-[5px] hover:bg-menu-hover"
                        hidden={entry.hidden}
                        onClick={() => call('menuPick', i)}
                    >
                        {entry.text}
                    </div>
                ),
            )}
        </div>
    );
}
