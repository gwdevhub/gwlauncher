import type { APIRoute } from 'astro';
import { getCollection } from 'astro:content';
import { navGroups } from '../lib/nav';
import { SITE } from '../lib/llms';
import siteConfig from '../../site.config.json';

// llmstxt.org index: a curated, link-first map of the docs for LLM agents.
export const GET: APIRoute = async () => {
  const entries = await getCollection('docs', ({ data }) => !data.hidden);
  const byId = new Map(entries.map((e) => [e.id, e]));

  const lines: string[] = [
    `# ${siteConfig.title}`,
    '',
    '> GW Launcher is a free, open-source Windows launcher for the original Guild Wars — store multiple accounts, log in and multi-box with a double-click, and auto-load DLL plugins and texture mods.',
    '',
    'Each page below is also available as raw Markdown by appending `.md` to its URL (e.g. /docs/faq.md). The full documentation concatenated into one file is at /llms-full.txt.',
    '',
  ];

  const seen = new Set<string>();
  for (const group of navGroups) {
    lines.push(`## ${group.label}`, '');
    for (const item of group.items) {
      const entry = byId.get(item.slug);
      if (!entry) continue;
      seen.add(entry.id);
      const desc = entry.data.description ? `: ${entry.data.description}` : '';
      lines.push(`- [${item.label}](${SITE}/docs/${item.slug}/)${desc}`);
    }
    lines.push('');
  }

  const leftover = entries
    .filter((e) => !seen.has(e.id))
    .sort((a, b) => a.data.title.localeCompare(b.data.title));
  if (leftover.length) {
    lines.push('## Other', '');
    for (const e of leftover) {
      const desc = e.data.description ? `: ${e.data.description}` : '';
      lines.push(`- [${e.data.title}](${SITE}/docs/${e.id}/)${desc}`);
    }
    lines.push('');
  }

  return new Response(lines.join('\n'), {
    headers: { 'Content-Type': 'text/plain; charset=utf-8' },
  });
};
