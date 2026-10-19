// The list's status line and selection survive the list being redrawn, as they did when they were page globals.
// A view sets them just before it asks for the list again; the list takes them once when it mounts.

export interface CarriedListState {
    status: string;
    selected: number | null;
}

let carried: CarriedListState = { status: '', selected: null };

export function carryListState(next: Partial<CarriedListState>) {
    carried = { status: next.status ?? '', selected: next.selected ?? null };
}

export function takeCarriedListState(): CarriedListState {
    const taken = carried;
    carried = { status: '', selected: null };
    return taken;
}
