export interface Output<T> {
    value: T;
}

export const UINT32_MAX = 0xFFFFFFFF;
const MAX_TIMEOUT_SECONDS = Math.floor((UINT32_MAX - 1) / 1000);

// Checked conversions leave the output unchanged on failure.
export function stringToFloat(text: string, output: Output<number>): boolean {
    if (text.trim() !== text || !/^-?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/.test(text)) {
        return false;
    }
    const value = Number(text);
    if (!Number.isFinite(value)) {
        return false;
    }
    output.value = value;
    return true;
}

// Round nonnegative finite values to the nearest integer, with halves rounded up.
export function floatToUint32(value: number, output: Output<number>): boolean {
    if (!Number.isFinite(value) || value < 0) {
        return false;
    }
    const rounded = Math.round(value);
    if (rounded > UINT32_MAX) {
        return false;
    }
    output.value = rounded;
    return true;
}

export function stringToUint32(text: string, output: Output<number>): boolean {
    if (text.trim() !== text || !/^\d+$/.test(text)) {
        return false;
    }
    return floatToUint32(Number(text), output);
}

export function parseTimeoutSeconds(text: string, milliseconds: Output<number>): boolean {
    const seconds = { value: 0 };
    if (!stringToFloat(text, seconds) || !floatToUint32(seconds.value, seconds) ||
        seconds.value < 1 || seconds.value > MAX_TIMEOUT_SECONDS) {
        return false;
    }
    milliseconds.value = seconds.value * 1000;
    return true;
}

export function timeoutSecondsText(milliseconds: number): string {
    return String(Math.max(1, Math.min(MAX_TIMEOUT_SECONDS, Math.round(milliseconds / 1000))));
}
