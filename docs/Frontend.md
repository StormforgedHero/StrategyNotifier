# GEM Frontend

Static, dependency-free viewer for Global Equities Momentum signals. It runs from `src/Frontend/Gem.Frontend/`, switches between profiles discovered in `dist/gem/profiles.json`, and loads each profile's `signals.json` as listed in the manifest.

## Data resolution
- Live manifest: resolved as `dist/gem/profiles.json` relative to the current page location (works at `/` locally and `/<repo>/` on GitHub Pages). Signals are resolved from that manifest's directory, so `eu.signals.json` becomes `.../dist/gem/eu.signals.json`.
- Demo fallback: if the live manifest fetch fails (404/network), a warning banner explains the issue and the page uses `src/Frontend/Gem.Frontend/demo/profiles.json` with the DEMO badge. "Refresh view" retries LIVE.
- The selected profile is persisted via `?profile=` in the URL and `localStorage`.
- Freshness: the status line shows LIVE/DEMO badge, "Generated" from the signals file (Last-Modified when available), and "Loaded" for the local fetch time. "Refresh view" re-fetches manifest and signals with cache-busting; CLI commands are needed to regenerate data.
- Pages verification: open the deployed `/app.js` on GitHub Pages and confirm `LIVE_MANIFEST_PATH` has no leading slash.

## Quick usage
- LIVE locally: generate data (`dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update`), then from repo root run `python -m http.server 8000` and open `http://localhost:8000/` (or `http://localhost:8000/src/Frontend/Gem.Frontend/`).
- DEMO locally without stopping the server: open `http://localhost:8000/?source=demo` (repo-root server) or serve only the frontend folder and open `http://localhost:8000/?source=demo`.
- Source switch: `?source=auto` (default, tries LIVE then DEMO), `?source=live` (LIVE only), `?source=demo` (DEMO only).
- DEMO on GitHub Pages: append `?source=demo` to the project URL.
- Suspect stale assets: hard-refresh or open in incognito to clear cached JS/CSS.

## Generate signals
- All profiles (offline): `dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update`
- All profiles update: `dotnet run --project src/Strategies/Gem/Gem.Cli -- --force-update`
- Single profile (offline): `dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update --profile=us`
- Single profile update: `dotnet run --project src/Strategies/Gem/Gem.Cli -- --force-update --profile=us`
- Legacy single config: `dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update --config=config/gem/gem.config.json`
- Both `--flag value` and `--flag=value` are supported; docs use the equals form for clarity.

## Run locally (real data)
1) Generate data: `dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update` (or `--force-update` for fresh data).
2) From the repository root: `python -m http.server 8000`
3) Open: `http://localhost:8000/` — the page loads live manifest/signals automatically with cache-busting on refresh.
4) The "Refresh view" button re-fetches manifest and signals without touching data; to regenerate data use the CLI commands above.

## Demo vs Live
- Demo (frontend folder only): `cd src/Frontend/Gem.Frontend && python -m http.server 8000`, then open `http://localhost:8000/` (do **not** append `/src/Frontend/Gem.Frontend/`). If you see the deep path in the address bar, replace it with `/`.
- Live (real data): serve the repo root (`python -m http.server 8000` from repo root) and open `http://localhost:8000/` so `dist/` is available.
