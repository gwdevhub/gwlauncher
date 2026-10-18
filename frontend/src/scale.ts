// The UI scale: the root element's zoom, so every size on the page scales together.
export function applyUiScale(percent: number) {
    document.documentElement.style.zoom = String(percent / 100);
}
