// A small note under or beside a control. It keeps the semantic `note` class for overrides.

import type { ReactNode } from 'react';
import { joinClasses } from '../utils/joinClasses';

export function Note({ children, className }: { children: ReactNode; className?: string }) {
    return <p className={joinClasses('note text-[11px] text-muted', className)}>{children}</p>;
}
