const MAX_TIMEOUT_SECONDS = 4294967;

export function parseTimeoutSeconds(text: string): number | undefined {
    const seconds = Math.round(Number(text));
    return Number.isSafeInteger(seconds) && seconds >= 1 && seconds <= MAX_TIMEOUT_SECONDS ? seconds * 1000 : undefined;
}

export function timeoutSecondsText(milliseconds: number): string {
    return String(Math.max(1, Math.min(MAX_TIMEOUT_SECONDS, Math.round(milliseconds / 1000))));
}
