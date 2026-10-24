export const UINT32_MAX = 0xFFFFFFFF;
const MAX_TIMEOUT_SECONDS = Math.floor((UINT32_MAX - 1) / 1000);

// Checked conversions return undefined when the input is invalid or out of range.
export function stringToFloat(text: string): number | undefined {
    if (text.trim() !== text || !/^-?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/.test(text)) {
        return undefined;
    }
    const value = Number(text);
    if (!Number.isFinite(value)) {
        return undefined;
    }
    return value;
}

// Round nonnegative finite values to the nearest integer, with halves rounded up.
export function floatToUint32(value: number): number | undefined {
    if (!Number.isFinite(value) || value < 0) {
        return undefined;
    }
    const rounded = Math.round(value);
    if (rounded > UINT32_MAX) {
        return undefined;
    }
    return rounded;
}

export function stringToUint32(text: string): number | undefined {
    if (text.trim() !== text || !/^\d+$/.test(text)) {
        return undefined;
    }
    return floatToUint32(Number(text));
}

export function parseTimeoutSeconds(text: string): number | undefined {
    const parsed = stringToFloat(text);
    const seconds = parsed === undefined ? undefined : floatToUint32(parsed);
    if (seconds === undefined || seconds < 1 || seconds > MAX_TIMEOUT_SECONDS) {
        return undefined;
    }
    return seconds * 1000;
}

export function timeoutSecondsText(milliseconds: number): string {
    return String(Math.max(1, Math.min(MAX_TIMEOUT_SECONDS, Math.round(milliseconds / 1000))));
}
