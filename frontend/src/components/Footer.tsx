// The build and operating system, one to a line, with the update icon on the right when a release is out.

import { call } from '../bridge';
 
export function Footer({ version, update }: { version?: string; update?: { revision: number } | null }) {
    return (
        <div className="footer flex items-center justify-between gap-1.5 bg-surface">
            <div className={"px-1.5 py-0.5"}>
                {(version ?? '').split(' · ').map((text, i) => (
                    <p key={i} className="note version m-0 text-[11px] leading-[1.1] text-version">
                        {text}
                    </p>
                ))}
            </div>
            {update && (
                <button
                    type="button"
                    className="update-icon flex gap-0.5 items-center cursor-pointer rounded-0 self-stretch border-0 hover:bg-green-700/10 px-1 leading-0 text-green-700 hover:text-green-800 focus-visible:bg-icon-surface focus-visible:text-green-800"
                    title={`Version r${update.revision} available`}
                    onClick={() => call('showUpdate')}
                ><div className={"text-[10px] text-center leading-[1.1]"}>Update<br/>Available</div>
                    <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
                        <path d="M8 2 L13 8 H10 V14 H6 V8 H3 Z" fill="currentColor" />
                    </svg>
                </button>
            )}
        </div>
    );
}
