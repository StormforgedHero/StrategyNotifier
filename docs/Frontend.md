# GEM Frontend

Static, dependency-free viewer for Global Equities Momentum signals. It runs from `src/Frontend/Gem.Frontend/`, switches between profiles discovered in `dist/gem/profiles.json`, and loads each profile's `signals.json` as listed in the manifest.

## Data resolution
- Live manifest: `dist/gem/profiles.json` (served from repo root). Signals are resolved relative to that manifest entry (`signalsPath`). No manual copying is needed. Data loads automatically on page load.
- Demo fallback: when the live manifest is missing, the frontend falls back to `src/Frontend/Gem.Frontend/demo/profiles.json` and shows clearly labeled demo data (DEMO badge).
- The selected profile is persisted via `?profile=` in the URL and `localStorage`.
- Freshness: the status line shows LIVE/DEMO badge, "Generated" from the signals file (Last-Modified when available), and "Loaded" for the local fetch time. "Refresh view" re-fetches manifest and signals with cache-busting; CLI commands are needed to regenerate data.

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
