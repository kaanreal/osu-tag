import { serve } from "https://deno.land/std@0.224.0/http/server.ts";

const clientId = Deno.env.get("SPOTIFY_CLIENT_ID") ?? "";
const clientSecret = Deno.env.get("SPOTIFY_CLIENT_SECRET") ?? "";

let accessToken: string | null = null;
let tokenExpiry = 0;
const cacheTtlMs = 24 * 60 * 60 * 1000;
const searchCache = new Map<string, { expiresAt: number; value: { found: boolean; url: string | null; name: string | null } }>();
const rateLimitWindowMs = 60 * 1000;
const rateLimitMax = 60;
const rateLimit = new Map<string, { count: number; resetAt: number }>();

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Headers": "authorization, apikey, content-type",
    },
  });
}

function getClientKey(req: Request) {
  return (
    req.headers.get("cf-connecting-ip") ??
    req.headers.get("x-forwarded-for") ??
    req.headers.get("x-real-ip") ??
    "unknown"
  ).split(",")[0].trim();
}

function rateLimitCheck(req: Request) {
  const key = getClientKey(req);
  const now = Date.now();
  const bucket = rateLimit.get(key);
  if (!bucket || now > bucket.resetAt) {
    rateLimit.set(key, { count: 1, resetAt: now + rateLimitWindowMs });
    return { allowed: true, retryAfterMs: 0 };
  }
  if (bucket.count >= rateLimitMax) {
    return { allowed: false, retryAfterMs: bucket.resetAt - now };
  }
  bucket.count += 1;
  return { allowed: true, retryAfterMs: 0 };
}

async function getAccessToken(): Promise<string | null> {
  if (!clientId || !clientSecret) return null;

  const now = Date.now();
  if (accessToken && now < tokenExpiry) return accessToken;

  const body = new URLSearchParams({
    grant_type: "client_credentials",
    client_id: clientId,
    client_secret: clientSecret,
  });

  const response = await fetch("https://accounts.spotify.com/api/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body,
  });

  if (!response.ok) return null;
  const json = await response.json();
  accessToken = json.access_token ?? null;
  tokenExpiry = now + (json.expires_in ?? 0) * 1000 - 60_000;
  return accessToken;
}

function cleanTitle(title: string) {
  return title.replace(/\(TV Size\)/gi, "").trim();
}

function cleanArtist(artist: string) {
  return artist.trim();
}

function removeParentheses(text: string) {
  return text.replace(/\s*\(.*?\)\s*/g, "").trim();
}

function removeBrackets(text: string) {
  return text.replace(/\s*\[.*?\]\s*/g, "").trim();
}

function removeFeat(text: string) {
  return text.replace(/\s*feat.*/i, "").trim();
}

function removeFt(text: string) {
  return text.replace(/\s*ft.*/i, "").trim();
}

async function executeSearch(query: string, token: string) {
  const escapedQuery = encodeURIComponent(query);
  const url =
    `https://api.spotify.com/v1/search?q=${escapedQuery}&type=track&limit=5&market=US`;
  const response = await fetch(url, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!response.ok) return null;
  const json = await response.json();
  return json?.tracks?.items ?? null;
}

function normalizeTitle(text: string) {
  const withoutParens = removeParentheses(text);
  const withoutBrackets = removeBrackets(withoutParens);
  return withoutBrackets.toLowerCase().replace(/\s+/g, " ").trim();
}

function normalizeArtist(text: string) {
  return text.toLowerCase().replace(/\s+/g, " ").trim();
}

function isTitleMatch(candidate: string, target: string) {
  if (!candidate || !target) return false;
  if (candidate === target) return true;
  if (candidate.startsWith(`${target} `)) return true;
  if (target.startsWith(`${candidate} `)) return true;
  return false;
}

function pickBestMatch(
  items: Array<{ name?: string; external_urls?: { spotify?: string }; artists?: Array<{ name?: string }> }>,
  title: string,
  artist: string,
) {
  const target = normalizeTitle(title);
  if (!target) return null;
  const targetArtist = normalizeArtist(artist);
  for (const item of items) {
    const name = item?.name ?? "";
    if (!name) continue;
    const normalized = normalizeTitle(name);
    if (!normalized) continue;
    const titleMatch = isTitleMatch(normalized, target);
    if (!titleMatch) continue;

    if (targetArtist) {
      const artists = item?.artists ?? [];
      const artistMatch = artists.some((a) => {
        const artistName = normalizeArtist(a?.name ?? "");
        return artistName && (artistName.includes(targetArtist) || targetArtist.includes(artistName));
      });
      if (!artistMatch) continue;
    }
    return item;
  }
  return null;
}

async function trySearchWithConditions(artist: string, title: string, token: string) {
  let result = await executeSearch(`artist:${artist} track:${title}`, token);
  if (result && result.length) return result;

  if (title.includes("(") && title.includes(")")) {
    const cleanTitle = removeParentheses(title);
    result = await executeSearch(`artist:${artist} track:${cleanTitle}`, token);
    if (result && result.length) return result;
  }

  if (title.includes("[") && title.includes("]")) {
    const cleanTitle = removeBrackets(title);
    result = await executeSearch(`artist:${artist} track:${cleanTitle}`, token);
    if (result && result.length) return result;
  }

  if (/feat/i.test(artist)) {
    const cleanArtist = removeFeat(artist);
    result = await executeSearch(`artist:${cleanArtist} track:${title}`, token);
    if (result && result.length) return result;
  }

  if (/ft/i.test(artist)) {
    const cleanArtist = removeFt(artist);
    result = await executeSearch(`artist:${cleanArtist} track:${title}`, token);
    if (result && result.length) return result;
  }

  return null;
}

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response(null, {
      status: 204,
      headers: {
        "Access-Control-Allow-Origin": "*",
        "Access-Control-Allow-Headers": "authorization, apikey, content-type",
      },
    });
  }

  if (req.method !== "POST") {
    return jsonResponse({ error: "Use POST" }, 405);
  }

  if (!clientId || !clientSecret) {
    return jsonResponse({ error: "Spotify credentials not configured" }, 500);
  }

  const limit = rateLimitCheck(req);
  if (!limit.allowed) {
    return jsonResponse(
      { error: "Rate limit exceeded", retry_after_ms: Math.max(0, Math.floor(limit.retryAfterMs)) },
      429,
    );
  }

  let payload: { artist?: string; title?: string };
  try {
    payload = await req.json();
  } catch {
    return jsonResponse({ error: "Invalid JSON" }, 400);
  }

  const artist = payload.artist?.trim() ?? "";
  const title = payload.title?.trim() ?? "";
  if (!artist || !title) {
    return jsonResponse({ error: "artist and title are required" }, 400);
  }

  const cacheKey = `${artist.toLowerCase()}|${title.toLowerCase()}`;
  const cached = searchCache.get(cacheKey);
  if (cached && cached.expiresAt > Date.now()) {
    return jsonResponse(cached.value);
  }

  const token = await getAccessToken();
  if (!token) {
    return jsonResponse({ error: "Failed to authenticate with Spotify" }, 500);
  }

  const cleanArtistValue = cleanArtist(artist);
  const cleanTitleValue = cleanTitle(title);
  const results = await trySearchWithConditions(cleanArtistValue, cleanTitleValue, token);

  if (results && results.length) {
    const exact = pickBestMatch(results, cleanTitleValue, cleanArtistValue) ?? results[0];
    const track = exact;
    const url = track?.external_urls?.spotify ?? null;
    if (!url) {
      const value = { found: false, url: null, name: null };
      searchCache.set(cacheKey, { expiresAt: Date.now() + cacheTtlMs, value });
      return jsonResponse(value);
    }
    const value = {
      found: true,
      url,
      name: track?.name ?? null,
    };
    searchCache.set(cacheKey, { expiresAt: Date.now() + cacheTtlMs, value });
    return jsonResponse(value);
  }

  const debug = new URL(req.url).searchParams.get("debug");
  if (debug === "1") {
    const value = {
      found: false,
      url: null,
      name: null,
      debug: {
        artist: cleanArtistValue,
        title: cleanTitleValue,
        resultCount: results?.length ?? 0,
        sampleNames: (results ?? []).slice(0, 5).map((r: { name?: string }) => r?.name ?? ""),
      },
    };
    searchCache.set(cacheKey, { expiresAt: Date.now() + cacheTtlMs, value: { found: false, url: null, name: null } });
    return jsonResponse(value);
  }

  const value = { found: false, url: null, name: null };
  searchCache.set(cacheKey, { expiresAt: Date.now() + cacheTtlMs, value });
  return jsonResponse(value);
});
