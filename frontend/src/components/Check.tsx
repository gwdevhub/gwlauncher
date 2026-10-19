// A checkbox with its label. It keeps the semantic `check` class for overrides.

import type { InputHTMLAttributes, ReactNode } from 'react';
import { joinClasses } from '../utils/joinClasses';

export function Check({ label, className, ...rest }: InputHTMLAttributes<HTMLInputElement> & { label: ReactNode; className?: string }) {
    return (
        <label className={joinClasses('check flex items-center gap-1.5', className)}>
            <input type="checkbox" {...rest} />
            {label}
        </label>
    );
}
