# gwlauncher.gwtoolbox.com — Astro site

The source for the GW Launcher documentation site at
[gwlauncher.gwtoolbox.com](https://gwlauncher.gwtoolbox.com). Built with
[Astro](https://astro.build) + Tailwind v4 + React (for the search overlay),
statically rendered, and deployed to GitHub Pages by
`.github/workflows/site-deploy.yml`.

It shares the visual language of the [GWToolbox++ site](https://www.gwtoolbox.com)
(same Fremont display face, dark glass theme) with its own steel-blue identity
matching the launcher's helmet icon.

## Local development

```bash
cd site
npm install
npm run dev      # http://localhost:4321
```

## Build

```bash
npm run build    # → site/dist (also generates the Pagefind search index)
npm run preview  # serve site/dist locally
```

`npm run build` runs `astro build` followed by `pagefind --site dist`. The
search overlay (`/` or ⌘K) needs the Pagefind index, so always use
`npm run build` rather than `astro build` directly when checking search.

## Editing content

All doc pages live under [`src/content/docs/`](src/content/docs/). To add a page:

1. Drop `new-page.mdx` (or `.md`) in `src/content/docs/`.
2. Give it front matter:
   ```yaml
   ---
   title: "Display title"
   description: "One-line summary for search/SEO"
   section: launching   # getting-started | accounts | launching | mods | reference
   ---
   ```
3. Add it to the sidebar by editing [`src/lib/nav.ts`](src/lib/nav.ts). Pages not
   in the manifest still resolve at `/docs/<slug>/`, they just don't appear in
   the sidebar.

Links between docs use absolute paths (`[Settings](/docs/settings/)`). MDX pages
can import the `LauncherPath` component to annotate where a control lives in the
launcher UI:

```mdx
import LauncherPath from '../../components/LauncherPath.astro';

<LauncherPath kind="menu" steps={['Right-click the list', 'Add New']} />
```

## Where things live

- `src/layouts/Base.astro` — site chrome (head, header, footer, background).
- `src/layouts/DocPage.astro` — sidebar + content wrapper used by every doc.
- `src/pages/index.astro` — landing page (hand-written).
- `src/pages/docs/[...slug].astro` — renders every entry from `src/content/docs/`.
- `src/components/` — `Header`, `Sidebar`, `Footer`, `PageBackground`, `Search.tsx`, `LauncherPath`.
- `src/lib/nav.ts` — sidebar manifest.
- `src/lib/release.ts` — pulls the latest release from the GitHub API at build
  time (with a static fallback so the build never breaks).
- `site.config.json` — non-secret site metadata (title, social links, maintainers).
- `public/` — static files served as-is (`favicon.ico`, `CNAME`, fonts, brand).

## Deployment & custom domain

The repo's **Settings → Pages** source must be set to **GitHub Actions**. The
workflow publishes on every push to `master` that touches `site/`, serving the
custom domain `gwlauncher.gwtoolbox.com` via `public/CNAME`. See
[`DEPLOYMENT.md`](DEPLOYMENT.md) for the one-time DNS + Pages setup.
