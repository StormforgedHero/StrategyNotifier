# Apps Script Runbook (subscriber notifier)
Canonical guide for recreating the Google Apps Script that syncs subscribers and sends monthly emails.

## Prerequisites
- Google account with access to Forms, Sheets, and Apps Script.
- New Google Sheet bound to a Google Form.
- Files from this repo: `scripts/apps-script/Main.gs`, `scripts/apps-script/appsscript.json`.
- Deployed privacy page URL that matches the site footer (for example, `https://<user>.github.io/StrategyNotifier/privacy.html`).
- The files under `scripts/apps-script/` are reference snapshots; edit and deploy inside the Apps Script project.

## Sheet tabs and headers (exact)
- `Form_Responses` (created by the form).
- `Subscribers`: `Email, ScheduleMode, DayOfMonth, WeekOfMonth, DayOfWeek, OffsetDays, Portfolios, RequestText, Active, CreatedAt, UpdatedAt, LastSentYm, LastSentAt, LastSentFingerprint`.
- `Profiles`: `ProfileCode, DisplayName, SignalsUrl, IsActive, PageUrl`.
- `Logs`: `Timestamp, RunId, Email, Action, Reason, Portfolios, MonthKey, Details`.

## Form structure
- `Email` (email).
- `Schedule mode` (options: `Day of month`, `Weekday rule`).
  - If Day of month: `Day of month` (1-31).
  - If Weekday rule: `Week of month` (1, 2, 3, 4, LAST), `Day of week` (MON-SUN), `Offset days` (integer, negatives allowed).
- `Portfolios` (checkboxes). Option labels must match `Profiles.DisplayName` or a stable prefix; never rely on tickers.
- `Request text` (long answer, optional).
After linking to the sheet, rename the responses tab to `Form_Responses`.

## Install the script
1) Open the sheet -> Extensions -> Apps Script (creates a container-bound project).  
2) Project Settings: timezone `Europe/Warsaw`, runtime V8.  
3) Paste `Main.gs` from `scripts/apps-script/Main.gs`.  
4) Replace manifest with `scripts/apps-script/appsscript.json` (Project Settings -> "Show appsscript.json").  
5) Save.

## Script Properties (Project Settings -> Script properties)
- `UNSUBSCRIBE_CONTACT`: required text shown in every email footer (configured per environment).
- `PRIVACY_POLICY_URL`: required public URL of the deployed `privacy.html` page.
- `SEND_ALLOWLIST` (optional): comma/semicolon separated emails. Empty/missing disables the restriction; helpful for test runs.

## Configuration constants (set in Main.gs)
- `DRY_RUN`: `true` = preview only, `false` = send.
- `DRY_RUN_UPDATES_LAST_SENT`: `true` also updates `LastSentYm/LastSentAt/LastSentFingerprint` in dry-run; default `false`.
- `MAX_SENDS_PER_MONTH_PER_EMAIL`: integer cap per email per month (default 2).

> Note: The files under `scripts/apps-script/` are reference snapshots. Edit constants and deploy inside the Apps Script project.

Recommended production (configure inside the Apps Script project):
- `DRY_RUN=false`, `DRY_RUN_UPDATES_LAST_SENT=false`.
- `MAX_SENDS_PER_MONTH_PER_EMAIL` set to your chosen cap (default 2 is conservative).
- Clear `SEND_ALLOWLIST` (use only for testing).
- Keep `PRIVACY_POLICY_URL` aligned with the site footer link.

## What the script does (per run)
1. Sync form responses into `Subscribers` by `Email`; log `SYNC` on create/update.  
2. Deactivate older duplicates (`DEDUP`).  
3. Compute "due today":  
   - `DAY_OF_MONTH`: clamp to month end.  
   - `WEEKDAY_RULE`: WeekOfMonth 1-4 or `LAST`, DayOfWeek MON-SUN, optional `OffsetDays`.  
4. Build a configuration fingerprint (schedule + portfolios).  
5. Apply monthly guard per email: enforce `MAX_SENDS_PER_MONTH_PER_EMAIL`; skip if the same fingerprint was already sent this month.  
6. Enforce `SEND_ALLOWLIST` if set; otherwise allow all active due rows.  
7. Map portfolio answers to `ProfileCode` via `Profiles.DisplayName`/prefix, require `IsActive=TRUE`, fetch latest signal from `SignalsUrl`, render allocations by instrument name (ticker as fallback).  
8. Email body: sections per profile; "View on page" links to `PageUrl` only (no JSON links); footer uses `UNSUBSCRIBE_CONTACT` and `PRIVACY_POLICY_URL`.  
9. Log actions to `Logs` (`RunId`, `SYNC`, `DUE`, `EMAIL_PREVIEW`, `SENT`, `SKIPPED`, `SEND_ERROR`, `EMAIL_PREVIEW_ERROR`, `DEDUP`, `ERROR`). Dry-run writes `EMAIL_PREVIEW`; if `DRY_RUN_UPDATES_LAST_SENT=true`, it also updates send markers (including fingerprint).

## Entry point
- `runOnceDryRun` — single entry point. Behavior depends on the `DRY_RUN` constant in `Main.gs`.

## Trigger
Create a time-driven trigger that runs `runOnceDryRun` once per day in the 19:00–20:00 window (Europe/Warsaw). Apps Script lets you pick an hourly window rather than an exact minute; this is expected.

## Manual test checklist
1. Confirm headers are exact; ensure `Profiles` has at least one active row.  
2. Submit the form (ideally covering both schedule modes).  
3. For manual testing, set `DRY_RUN=true` and optionally `SEND_ALLOWLIST` to a test email. Run `runOnceDryRun`; check `Logs` for `SYNC`, `DUE`, `EMAIL_PREVIEW`; verify `LastSentYm` stays empty unless `DRY_RUN_UPDATES_LAST_SENT=true`.  
4. For production, set `DRY_RUN=false` (and clear `SEND_ALLOWLIST`), then run `runOnceDryRun` to send; verify `LastSentYm/LastSentAt/LastSentFingerprint` updated.  
5. Keep `DRY_RUN=false` in production; use `SEND_ALLOWLIST` temporarily only for tests.

## Google Form – required question titles (must match exactly)
- `Sygnatura czasowa`
- `Adres e-mail`
- `Wybór portfoliów`
- `Wybierz dzień miesiąca`
- `Jak wyznaczyć termin wysyłki w miesiącu?`
- `Który tydzień miesiąca?`
- `Jaki dzień tygodnia?`
- `Kiedy wysłać względem wybranego terminu?`

Changing question titles or form language requires updating `Main.gs` in the Apps Script project (not covered by this repo snapshot).

## Troubleshooting
- Missing headers/tab names -> "Missing sheet/column": verify exact casing/order.  
- No sends -> see `Logs` for `SKIPPED` reasons (already sent, not in allowlist, no portfolios, inactive profile, monthly cap, fingerprint already sent).  
- Portfolio mapping issues -> align form labels with `Profiles.DisplayName`/prefix; ensure `IsActive=TRUE`.  
- Privacy link mismatch -> `PRIVACY_POLICY_URL` must match the deployed `privacy.html`; site footer uses a relative `./privacy.html` link for GitHub Pages compatibility.  
- Duplicates -> older entries are deactivated automatically; keep one active row per email.
