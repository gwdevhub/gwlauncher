// Small formatting helpers shared by the views.

// The class that colours an account's state: green when active, orange when in use, grey otherwise.
export function stateClass(state: string): string {
    const colour: Record<string, string> = {
        Active: 'state-active text-green-700',
        'In Use': 'state-in-use text-orange-700',
    };
    return `state whitespace-nowrap text-xs empty:hidden ${colour[state] ?? 'state-inactive text-muted'}`;
}

export const fileName = (path: string) => path.split(/[\\/]/).pop() ?? path;
export const isDll = (path: string) => /\.dll$/i.test(path);
export const DLL = 1;
