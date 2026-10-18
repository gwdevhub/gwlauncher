/**
 * Fetches the latest GW Launcher release from GitHub at build time, so the
 * download links always point at the freshest assets. The page picks the
 * Windows or Linux link for each visitor in the browser (see DownloadLinks).
 *
 * Falls back to the pinned URL in site.config.json if the API call fails
 * (rate limit, network) so the build never breaks.
 */
import siteConfig from '../../site.config.json' with { type: 'json' };

export type Downloads = {
  windows: string;
  linux: string;
};

export type LatestRelease = {
  version: string;
  downloads: Downloads;
  publishedAt: string | null;
  source: 'github' | 'fallback';
};

const FALLBACK: LatestRelease = {
  version: siteConfig.launcherVersion,
  downloads: {
    windows: siteConfig.fallbackDownloadUrl,
    linux: siteConfig.fallbackDownloadUrl,
  },
  publishedAt: null,
  source: 'fallback',
};

// The release assets by exact name, as the release workflow publishes them.
const WINDOWS_ASSET = 'GW_Launcher.exe';
const LINUX_ASSET = 'GW_Launcher';

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
    const assetUrl = (name: string) =>
      latest.assets?.find((a) => a.name === name)?.browser_download_url;
    // A missing asset sends that platform to the release page instead.
    const releasePage = latest.html_url ?? FALLBACK.downloads.windows;
    // Tags look like "r17.18" / names like "Release 17.18" — reduce to "17.18".
    const version = (latest.name ?? latest.tag_name ?? FALLBACK.version)
      .replace(/^release\s*/i, '')
      .replace(/^r(?=\d)/i, '')
      .replace(/^v(?=\d)/i, '')
      .trim();
    return {
      version,
      downloads: {
        windows: assetUrl(WINDOWS_ASSET) ?? releasePage,
        linux: assetUrl(LINUX_ASSET) ?? releasePage,
      },
      publishedAt: latest.published_at ?? null,
      source: 'github',
    };
  } catch {
    return FALLBACK;
  }
}
