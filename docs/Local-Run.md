# Local run and refreshing profiles

Use these commands to regenerate signals locally.

## Regenerate all profiles (no network)
```bash
dotnet run --project src/Strategies/Gem/Gem.Cli -- --no-update
```

## Refresh a single profile (force update)
```bash
dotnet run --project src/Strategies/Gem/Gem.Cli -- --force-update --profile=<PROFILE_CODE>
```

Replace `<PROFILE_CODE>` with the profile id from `config/gem/profiles/*.profile.json` (for example, `us`).

Serve the repo root to view the updated static site locally:
```bash
python -m http.server 8000
# then open http://localhost:8000/
```
