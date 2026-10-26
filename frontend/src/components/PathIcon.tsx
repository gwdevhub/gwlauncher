import { useEffect, useState } from 'react';
import { call } from '../bridge';

const pending = new Map<string, Promise<number[] | null>>();

function loadIcon(path: string): Promise<number[] | null> {
    let request = pending.get(path);
    if (!request) {
        request = call<number[] | null>('pathIcon', path).finally(() => pending.delete(path));
        pending.set(path, request);
    }
    return request;
}

export function PathIcon({ path, fallback, className }: { path?: string | null; fallback?: string; className?: string }) {
    const [image, setImage] = useState<{ path: string; url: string } | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let alive = true;
        let url: string | undefined;
        setImage(null);
        setError(null);
        if (path) {
            loadIcon(path).then(bytes => {
                if (!alive || !bytes?.length) return;
                url = URL.createObjectURL(new Blob([new Uint8Array(bytes).buffer], { type: 'image/png' }));
                setImage({ path, url });
            }).catch(error => {
                if (alive) setError(`Could not load icon: ${String(error)}`);
            });
        }
        return () => {
            alive = false;
            if (url) URL.revokeObjectURL(url);
        };
    }, [path]);

    return <img className={className} src={image && image.path === path ? image.url : fallback} alt="" title={error ?? undefined} />;
}
