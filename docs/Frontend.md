# GEM Frontend

Static, dependency-free viewer for Global Equities Momentum signals. It runs from `src/Frontend/Gem.Frontend/`, switches between profiles discovered in `dist/gem/profiles.json`, and loads each profile's `signals.json` as listed in the manifest.

## Data resolution
- Live manifest: resolved as `dist/gem/profiles.json` relative to the current page location (works at `/` locally and `/<repo>/` on GitHub Pages). Signals are resolved from that manifest's directory, so `eu.signals.json` becomes `.../dist/gem/eu.signals.json`.
- Demo fallback: if the live manifest fetch fails (404/network), a warning banner explains the issue and the page uses `src/Frontend/Gem.Frontend/demo/profiles.json` with the Demo badge. "Refresh view" retries Live.
- The selected profile is persisted via `?profile=` in the URL and `localStorage`.
- Freshness: the status line shows Live/Demo badge, "Generated" from the signals file (Last-Modified when available), and "Loaded" for the local fetch time. "Refresh view" re-fetches manifest and signals with cache-busting; CLI commands are needed to regenerate data.
- Pages verification: open the deployed `/app.js` on GitHub Pages and confirm `LIVE_MANIFEST_PATH` has no leading slash.

## Quick usage
- Live locally: generate data, then serve the repo root and open `http://localhost:8000/`. See [Local run](Local-Run.md) for commands.
- Demo locally without stopping the server: open `http://localhost:8000/?source=demo` (repo-root server) or serve only the frontend folder and open `http://localhost:8000/?source=demo`.
- Source switch: `?source=auto` (default, tries Live then Demo), `?source=live` (Live only), `?source=demo` (Demo only).
- Demo on GitHub Pages: append `?source=demo` to the project URL.
- Suspect stale assets: hard-refresh or open in incognito to clear cached JS/CSS.

## Demo vs Live
- Demo (frontend folder only): `cd src/Frontend/Gem.Frontend && python -m http.server 8000`, then open `http://localhost:8000/` (do **not** append `/src/Frontend/Gem.Frontend/`). If you see the deep path in the address bar, replace it with `/`.
- Live (real data): serve the repo root (`python -m http.server 8000` from repo root) and open `http://localhost:8000/` so `dist/` is available.
- For regenerating signals, see [Local run](Local-Run.md).
