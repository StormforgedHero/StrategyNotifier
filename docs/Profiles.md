# GEM profiles

Profiles define self-contained GEM runs backed by their own `outputPath` under `dist/gem/<id>.signals.json`. Files live under `config/gem/profiles/` and follow the same schema as `config/gem/gem.config.json`; the loader rejects extra fields.

## Naming and folders
- Flat files under `config/gem/profiles/`.
- Use short ids with `.profile.json` suffix, e.g., `us.profile.json`, `eu.profile.json`, `pl.profile.json`.
- Keep `storeDirectory` as `data/gem/sample` and `cacheDirectory` as `data/gem/cache` for offline runs.
- Use a unique `outputPath` per profile, e.g., `dist/gem/us.signals.json`.

## Adding a new profile
1) Create `config/gem/profiles/<id>.profile.json` matching the allowed schema.
2) Set `outputPath` to a unique file such as `dist/gem/<id>.signals.json`.
3) Keep `update.autoUpdateEnabled` disabled for deterministic offline runs unless you intentionally turn it on.

## Batch generation
- Run all profiles at once (default flow): `dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update`
- Update all: `dotnet run --project src/Strategies/Gem/Gem.Cli -- --force-update`
- The CLI discovers `*.profile.json` (and legacy `*.gem.config.json`) under `config/gem/profiles` by default, generates each profile's `signals.json`, and writes `dist/gem/profiles.json` for the frontend.
- Single profile (offline): `dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update --profile=us`
- Single profile update: `dotnet run --project src/Strategies/Gem/Gem.Cli -- --force-update --profile=us`
- Legacy single config remains available: `dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update --config=config/gem/gem.config.json`
