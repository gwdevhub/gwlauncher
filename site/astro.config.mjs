import { defineConfig } from 'astro/config';
import react from '@astrojs/react';
import mdx from '@astrojs/mdx';
import sitemap from '@astrojs/sitemap';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  site: 'https://gwlauncher.gwtoolbox.com',
  integrations: [react(), mdx(), sitemap()],
  vite: {
    plugins: [tailwindcss()],
    server: {
      fs: {
        // Some checkouts live under a junctioned/symlinked path (e.g. a Windows
        // "Git Repositories" dir that realpaths to a different no-space path).
        // Vite compares its fs allow-list (the symlinked root) against the
        // realpath'd module path and rejects the mismatch, which breaks the dev
        // toolbar/HMR client. Relaxing strict mode fixes it. Dev-server only —
        // the static production build is unaffected.
        strict: false,
      },
    },
  },
  markdown: {
    shikiConfig: {
      theme: 'github-dark-dimmed',
      wrap: true,
    },
  },
});
