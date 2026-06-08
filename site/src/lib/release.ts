/**
 * Fetches the latest GW Launcher release from GitHub at build time so the
 * download button always points at the freshest asset.
 *
 * Falls back to the pinned URL in site.config.json if the API call fails
 * (rate limit, network) so the build never breaks.
 */
import siteConfig from '../../site.config.json' with { type: 'json' };

export type LatestRelease = {
  version: string;
  url: string;
  publishedAt: string | null;
  source: 'github' | 'fallback';
};

const FALLBACK: LatestRelease = {
  version: siteConfig.launcherVersion,
  url: siteConfig.fallbackDownloadUrl,
  publishedAt: null,
  source: 'fallback',
};

export async function getLatestRelease(): Promise<LatestRelease> {
  try {
    const headers: Record<string, string> = {
      Accept: 'application/vnd.github+json',
      'User-Agent': 'gwlauncher-site-build',
    };
    if (process.env.GITHUB_TOKEN) {
      headers.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`;
    }
    const res = await fetch(
      'https://api.github.com/repos/gwdevhub/gwlauncher/releases?per_page=10',
      { headers },
    );
    if (!res.ok) return FALLBACK;
    const releases = (await res.json()) as {
      tag_name?: string;
      name?: string;
      html_url?: string;
      published_at?: string;
      draft?: boolean;
      prerelease?: boolean;
      assets?: { name: string; browser_download_url: string }[];
    }[];
    const published = releases.filter((r) => !r.draft && !r.prerelease);
    if (published.length === 0) return FALLBACK;
    const latest = published[0];
    // Prefer the self-contained build (no .NET runtime required). Its asset is
    // plain `GW_Launcher.exe`; the framework-dependent one carries "Framework"
    // in the name. Fall back to any .exe, then to the release page itself.
    const exes = latest.assets?.filter((a) => a.name.toLowerCase().endsWith('.exe')) ?? [];
    const exe = exes.find((a) => !/framework/i.test(a.name)) ?? exes[0];
    // Tags look like "r17.18" / names like "Release 17.18" — reduce to "17.18".
    const version = (latest.name ?? latest.tag_name ?? FALLBACK.version)
      .replace(/^release\s*/i, '')
      .replace(/^r(?=\d)/i, '')
      .replace(/^v(?=\d)/i, '')
      .trim();
    return {
      version,
      url: exe?.browser_download_url ?? latest.html_url ?? FALLBACK.url,
      publishedAt: latest.published_at ?? null,
      source: 'github',
    };
  } catch {
    return FALLBACK;
  }
}
