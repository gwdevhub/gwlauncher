/**
 * Navigation manifest — the order pages appear in the sidebar.
 * Each entry is a slug from src/content/docs. Slugs not listed here
 * still resolve, they just don't appear in the sidebar groups.
 */
export type NavGroup = {
  label: string;
  items: { slug: string; label: string }[];
};

export const navGroups: NavGroup[] = [
  {
    label: 'Getting Started',
    items: [
      { slug: 'installation', label: 'Install & First Launch' },
      { slug: 'multiboxing', label: 'Run Multiple Accounts' },
      { slug: 'faq', label: 'FAQ' },
    ],
  },
  {
    label: 'Accounts',
    items: [
      { slug: 'accounts', label: 'Managing Accounts' },
      { slug: 'steam', label: 'Steam Accounts' },
      { slug: 'encryption', label: 'Encryption & Passwords' },
    ],
  },
  {
    label: 'Launching',
    items: [
      { slug: 'launching', label: 'Launching Clients' },
      { slug: 'updating', label: 'Installing & Updating GW' },
      { slug: 'tray', label: 'Tray & Shortcuts' },
    ],
  },
  {
    label: 'Mods',
    items: [
      { slug: 'mods', label: 'Mods Overview' },
      { slug: 'plugins', label: 'DLL Plugins' },
      { slug: 'texmods', label: 'TexMods' },
    ],
  },
  {
    label: 'Reference',
    items: [
      { slug: 'settings', label: 'Settings' },
      { slug: 'files', label: 'Files & Storage' },
      { slug: 'troubleshooting', label: 'Troubleshooting' },
    ],
  },
];

export function findInNav(slug: string): { group: string; label: string } | null {
  for (const g of navGroups) {
    const item = g.items.find((i) => i.slug === slug);
    if (item) return { group: g.label, label: item.label };
  }
  return null;
}
