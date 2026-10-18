// The account list's context menu. The menu's own window reports the index of the item picked; the actions stay here.

let actions: (() => void)[] = [];

export function setMenuActions(next: (() => void)[]) {
    actions = next;
}

export function runMenuAction(index: number) {
    actions[index]?.();
}
