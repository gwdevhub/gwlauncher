// The launcher's progress bar. With a fraction it fills to that share; with none it runs an animation, for work with no known end.

import { progressPercent } from '../utils/format';

export function ProgressBar({ fraction, label }: { fraction?: number | null; label: string }) {
    const percent = progressPercent(fraction);
    const known = percent !== undefined;
    return (
        <div className="progress-track" role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent}>
            {known ? (
                <div className="progress-fill" style={{ width: `${percent}%` }} />
            ) : (
                <div className="progress-fill progress-indeterminate" />
            )}
        </div>
    );
}
