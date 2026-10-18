// Reports the element's size to the host, so an autosize window fits it. The size is in CSS pixels, which include the UI scale:
// the root's zoom is what the element's bounding box measures. The element must be sized by its content, not by the window.

import { useLayoutEffect } from 'react';
import { call } from './bridge';

export function useAutosize(element: HTMLElement | null) {
    useLayoutEffect(() => {
        if (!element) {
            return;
        }
        const report = () => {
            const box = element.getBoundingClientRect();
            call('fitToPage', Math.ceil(box.width), Math.ceil(box.height));
        };
        const observer = new ResizeObserver(report);
        observer.observe(element);
        return () => observer.disconnect();
    }, [element]);
}
