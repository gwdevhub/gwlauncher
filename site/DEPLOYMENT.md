# Deploying the docs site to `gwlauncher.gwtoolbox.com`

The site is a static Astro build published to **GitHub Pages** by
`.github/workflows/site-deploy.yml` on every push to `master` that touches
`site/`. It serves the custom subdomain **`gwlauncher.gwtoolbox.com`** — set up
the same way the main GWToolbox++ site serves `www.gwtoolbox.com`.

This is a one-time setup. After it's done, every push that changes `site/`
redeploys automatically.

## 1. Enable GitHub Pages on the repo

In **`gwdevhub/gwlauncher` → Settings → Pages**:

- **Build and deployment → Source:** select **GitHub Actions**.

(Don't pick "Deploy from a branch" — this repo deploys via the workflow.)

## 2. Add the DNS record

In the DNS zone for **`gwtoolbox.com`** (wherever it's hosted — e.g. Cloudflare),
add a single record:

| Type | Name (host) | Value / target | Proxy |
| --- | --- | --- | --- |
| `CNAME` | `gwlauncher` | `gwdevhub.github.io` | DNS only (grey cloud, if Cloudflare) |

That maps `gwlauncher.gwtoolbox.com` → the GitHub Pages servers for the
`gwdevhub` org. GitHub then routes it to this repo because of the `CNAME` file
below.

> The `gwlauncher` host is independent of the existing `www`, `tts`, `lfg`, and
> `kamadan` records — adding it doesn't affect any of them.

## 3. Confirm the custom domain

The repo already ships [`site/public/CNAME`](public/CNAME) containing
`gwlauncher.gwtoolbox.com`, which Astro copies to the published root. After the
first successful deploy, **Settings → Pages → Custom domain** should show
`gwlauncher.gwtoolbox.com` with a green check.

Once DNS has propagated and the certificate is issued, tick **Enforce HTTPS**.

## 4. Trigger the first deploy

Push the `site/` directory (and the workflow) to `master`, or run the
**"Deploy site to GitHub Pages"** workflow manually via **Actions →
workflow_dispatch**. Watch it under the repo's **Actions** tab; when it's green,
the site is live at <https://gwlauncher.gwtoolbox.com>.

## Notes

- Only changes under `site/**` (or the workflow file) trigger a deploy — the
  existing **Build and Release** workflow for the launcher is unaffected, since
  it filters on `GW Launcher/**`.
- DNS propagation can take anywhere from a few minutes to a couple of hours.
- If the cert is slow to issue, removing and re-adding the custom domain in
  Settings → Pages usually nudges GitHub to re-provision it.
