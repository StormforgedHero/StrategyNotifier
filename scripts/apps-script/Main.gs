/***************
 * CONFIG
 ***************/
const SHEET_FORM_RESPONSES = 'Form_Responses';
const SHEET_SUBSCRIBERS = 'Subscribers';
const SHEET_LOGS = 'Logs';
const SHEET_PROFILES = 'Profiles';

// Nazwa nadawcy (wyświetlana w mailu)
const SENDER_NAME = 'Strategy Notifier';

// Script Properties keys (Project Settings -> Script Properties)
const PROP_UNSUBSCRIBE_CONTACT = 'UNSUBSCRIBE_CONTACT';
const PROP_PRIVACY_POLICY_URL = 'PRIVACY_POLICY_URL';
const PROP_SEND_ALLOWLIST = 'SEND_ALLOWLIST'; // comma/semicolon-separated list, e.g. "a@b.com, c@d.com"

// DRY_RUN: true = brak wysyłki maili (na razie tylko logi)
const DRY_RUN = false;

// Jeśli chcesz testować anty-duplikację już na DRY_RUN:
// true = aktualizuje LastSentYm/LastSentAt także w DRY_RUN
const DRY_RUN_UPDATES_LAST_SENT = false;

// Limit: ile maili max / email / miesiąc (gdy użytkownik zmienia konfigurację)
const MAX_SENDS_PER_MONTH_PER_EMAIL = 5;

/***************
 * ENTRYPOINTS
 ***************/
function runOnceDryRun() {
  const runId = Utilities.getUuid();
  const now = new Date();

  // 1) Sync subscribers from form responses
  const syncResult = syncSubscribersFromFormResponses_(runId, now);

  const dedupResult = deduplicateSubscribers_(runId, now);

  // 2) Evaluate who is due today (dry run)
  const profiles = loadProfiles_();
  const evalResult = evaluateDueAndLog_(runId, now, profiles);

  // Summary log
  appendLog_({
    timestamp: now,
    runId,
    email: '',
    action: 'SUMMARY',
    reason: 'run completed',
    portfolios: '',
    monthKey: getMonthKey_(now),
    details: `sync: created=${syncResult.created}, updated=${syncResult.updated}; eval: due=${evalResult.due}, skipped=${evalResult.skipped}, errors=${evalResult.errors}`
  });
}

/***************
 * SYNC: Form_Responses -> Subscribers (upsert by Email)
 ***************/
function syncSubscribersFromFormResponses_(runId, now) {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  const shResp = ss.getSheetByName(SHEET_FORM_RESPONSES);
  const shSubs = ss.getSheetByName(SHEET_SUBSCRIBERS);

  if (!shResp) throw new Error(`Missing sheet: ${SHEET_FORM_RESPONSES}`);
  if (!shSubs) throw new Error(`Missing sheet: ${SHEET_SUBSCRIBERS}`);

  const respValues = shResp.getDataRange().getValues();
  if (respValues.length < 2) {
    appendLog_({
      timestamp: now, runId, email: '', action: 'SYNC', reason: 'no responses',
      portfolios: '', monthKey: getMonthKey_(now), details: 'Form_Responses has no data rows'
    });
    return { created: 0, updated: 0 };
  }

  const respHeaders = buildHeaderIndex_(respValues[0]);

  const subsValues = shSubs.getDataRange().getValues();
  if (subsValues.length < 1) throw new Error('Subscribers sheet has no header row.');
  const subsHeaders = buildHeaderIndex_(subsValues[0]);

  // Required Subscribers headers (fail fast if a column name changed)
  const reqSubs = [
    'Email', 'Active', 'FormTimestamp',
    'ScheduleMode', 'DayOfMonth', 'WeekOfMonth', 'DayOfWeek', 'OffsetDays',
    'Portfolios', 'RequestText',
    'CreatedAt', 'UpdatedAt',
    // opcjonalnie, ale zwykle masz te kolumny:
    'LastSentYm', 'LastSentAt', 'LastSentFingerprint'
  ];
  for (const h of reqSubs) {
    if (subsHeaders.get(h) === undefined) {
      throw new Error(`Subscribers missing required header: ${h}`);
    }
  }


  // ---- Helper: normalize timestamp from Forms/Sheets to comparable key + value to store
  function normalizeFormTs_(v) {
    const d = toDateOrNull_(v);
    if (d) return { key: String(d.getTime()), value: d };
    const s = String(v ?? '').trim();
    return { key: s, value: s };
  }

  // ---- 1) Build latest response per email (avoid multiple operations per email per run)
  const latestByEmail = new Map(); // email -> { row, tsKey, tsValue, tsDateMs }

  for (let r = 1; r < respValues.length; r++) {
    const row = respValues[r];
    const email = getResp_(row, respHeaders, 'Adres e-mail').trim().toLowerCase();
    if (!email) continue;

    const rawTs = getRespRaw_(row, respHeaders, 'Sygnatura czasowa');
    const normTs = normalizeFormTs_(rawTs);
    const tsMs = toDateOrNull_(normTs.value)?.getTime() ?? -1;

    const existing = latestByEmail.get(email);
    if (!existing) {
      latestByEmail.set(email, { row, tsKey: normTs.key, tsValue: normTs.value, tsMs });
      continue;
    }

    // Prefer larger timestamp (or if cannot parse -> prefer later row by r, but tsMs=-1 then)
    if (tsMs > existing.tsMs) {
      latestByEmail.set(email, { row, tsKey: normTs.key, tsValue: normTs.value, tsMs });
    }
  }

  // ---- 2) Build map: email -> best ACTIVE subscriber row
  const activeByEmail = new Map(); // email -> { sheetRow, score }

  for (let r = 1; r < subsValues.length; r++) {
    const row = subsValues[r];

    const email = String(row[subsHeaders.get('Email')] || '').trim().toLowerCase();
    if (!email) continue;

    const active = toBool_(row[subsHeaders.get('Active')]);
    if (!active) continue;

    const formTsMs = toDateOrNull_(row[subsHeaders.get('FormTimestamp')])?.getTime() ?? 0;
    const updatedAt = toDateOrNull_(row[subsHeaders.get('UpdatedAt')])?.getTime() ?? 0;
    const createdAt = toDateOrNull_(row[subsHeaders.get('CreatedAt')])?.getTime() ?? 0;

    // Preferujemy najnowsze zgłoszenie z formularza
    const score = formTsMs || Math.max(updatedAt, createdAt);

    const prev = activeByEmail.get(email);
    if (!prev || score > prev.score) {
      activeByEmail.set(email, { sheetRow: r + 1, score });
    }
  }

  let created = 0;
  let updated = 0;

  // ---- 3) Apply changes per email (latest response only)
  for (const [email, resp] of latestByEmail.entries()) {
    const row = resp.row;

    const scheduleText = getResp_(row, respHeaders, 'Jak wyznaczyć termin wysyłki w miesiącu?');
    const scheduleMode = mapScheduleMode_(scheduleText);

    const dayOfMonth = parseOptionalInt_(getResp_(row, respHeaders, 'Wybierz dzień miesiąca'));
    const weekOfMonth = mapWeekOfMonth_(getResp_(row, respHeaders, 'Który tydzień miesiąca?'));
    const dayOfWeek = mapDayOfWeek_(getResp_(row, respHeaders, 'Jaki dzień tygodnia?'));
    const offsetDays = mapOffsetDays_(getResp_(row, respHeaders, 'Kiedy wysłać względem wybranego terminu?'));

    const portfolios = getResp_(row, respHeaders, 'Wybór portfoliów');
    const requestText = getResp_(row, respHeaders, "Jeśli wybrałeś 'Inne / dodaj propozycję' - opisz krótko (opcjonalnie)");

    const existing = activeByEmail.get(email);

    // ---- A) No active subscriber => create new row
    if (!existing) {
      const newRow = new Array(shSubs.getLastColumn()).fill('');

      setByHeader_(newRow, subsHeaders, 'Email', email);
      setByHeader_(newRow, subsHeaders, 'FormTimestamp', resp.tsValue);

      setByHeader_(newRow, subsHeaders, 'ScheduleMode', scheduleMode);
      setByHeader_(newRow, subsHeaders, 'DayOfMonth', dayOfMonth ?? '');
      setByHeader_(newRow, subsHeaders, 'WeekOfMonth', weekOfMonth ?? '');
      setByHeader_(newRow, subsHeaders, 'DayOfWeek', dayOfWeek ?? '');
      setByHeader_(newRow, subsHeaders, 'OffsetDays', offsetDays ?? '');
      setByHeader_(newRow, subsHeaders, 'Portfolios', portfolios);
      setByHeader_(newRow, subsHeaders, 'RequestText', requestText);

      setByHeader_(newRow, subsHeaders, 'Active', true);
      setByHeader_(newRow, subsHeaders, 'CreatedAt', resp.tsValue || now);
      setByHeader_(newRow, subsHeaders, 'UpdatedAt', now);

      shSubs.appendRow(newRow);
      created++;

      appendLog_({
        timestamp: now, runId, email, action: 'SYNC', reason: 'created',
        portfolios, monthKey: getMonthKey_(now), details: `scheduleMode=${scheduleMode}`
      });

      continue;
    }

    // ---- B) Active subscriber exists => check FormTimestamp
    const existingRow = existing.sheetRow;
    const subsRowValues = shSubs.getRange(existingRow, 1, 1, shSubs.getLastColumn()).getValues()[0];

    const existingFormTsNorm = normalizeFormTs_(subsRowValues[subsHeaders.get('FormTimestamp')]);
    const existingFormTsKey = existingFormTsNorm.key;
    const incomingFormTsKey = resp.tsKey;

    const hasExistingFormTs = Boolean(String(existingFormTsKey || '').trim());

    // Migration-friendly: if existing FormTimestamp empty -> treat as update + backfill FormTimestamp
    const shouldUpdateInPlace = (!hasExistingFormTs) || (existingFormTsKey && incomingFormTsKey && existingFormTsKey === incomingFormTsKey);

    if (shouldUpdateInPlace) {
      setByHeader_(subsRowValues, subsHeaders, 'FormTimestamp', resp.tsValue);

      setByHeader_(subsRowValues, subsHeaders, 'ScheduleMode', scheduleMode);
      setByHeader_(subsRowValues, subsHeaders, 'DayOfMonth', dayOfMonth ?? '');
      setByHeader_(subsRowValues, subsHeaders, 'WeekOfMonth', weekOfMonth ?? '');
      setByHeader_(subsRowValues, subsHeaders, 'DayOfWeek', dayOfWeek ?? '');
      setByHeader_(subsRowValues, subsHeaders, 'OffsetDays', offsetDays ?? '');
      setByHeader_(subsRowValues, subsHeaders, 'Portfolios', portfolios);
      setByHeader_(subsRowValues, subsHeaders, 'RequestText', requestText);

      setByHeader_(subsRowValues, subsHeaders, 'UpdatedAt', now);

      shSubs.getRange(existingRow, 1, 1, shSubs.getLastColumn()).setValues([subsRowValues]);
      updated++;

      appendLog_({
        timestamp: now, runId, email, action: 'SYNC', reason: 'updated',
        portfolios, monthKey: getMonthKey_(now), details: `scheduleMode=${scheduleMode}`
      });

      continue;
    }

    // ---- C) FormTimestamp differs => VERSION BUMP (deactivate old + append new)
    // 1) deactivate old
    setByHeader_(subsRowValues, subsHeaders, 'Active', false);
    setByHeader_(subsRowValues, subsHeaders, 'UpdatedAt', now);
    shSubs.getRange(existingRow, 1, 1, shSubs.getLastColumn()).setValues([subsRowValues]);
    updated++;

    appendLog_({
      timestamp: now,
      runId,
      email,
      action: 'VERSIONED',
      reason: 'new submission for existing email',
      portfolios: String(subsRowValues[subsHeaders.get('Portfolios')] || ''),
      monthKey: getMonthKey_(now),
      details: `oldFormTs=${existingFormTsKey} -> newFormTs=${incomingFormTsKey}`
    });

    // 2) append new (nie kopiujemy LastSent*, aby zmiana konfiguracji mogła wysłać nową notyfikację)
    const newRow = new Array(shSubs.getLastColumn()).fill('');

    setByHeader_(newRow, subsHeaders, 'Email', email);
    setByHeader_(newRow, subsHeaders, 'FormTimestamp', resp.tsValue);

    setByHeader_(newRow, subsHeaders, 'ScheduleMode', scheduleMode);
    setByHeader_(newRow, subsHeaders, 'DayOfMonth', dayOfMonth ?? '');
    setByHeader_(newRow, subsHeaders, 'WeekOfMonth', weekOfMonth ?? '');
    setByHeader_(newRow, subsHeaders, 'DayOfWeek', dayOfWeek ?? '');
    setByHeader_(newRow, subsHeaders, 'OffsetDays', offsetDays ?? '');
    setByHeader_(newRow, subsHeaders, 'Portfolios', portfolios);
    setByHeader_(newRow, subsHeaders, 'RequestText', requestText);

    setByHeader_(newRow, subsHeaders, 'Active', true);
    setByHeader_(newRow, subsHeaders, 'CreatedAt', resp.tsValue || now);
    setByHeader_(newRow, subsHeaders, 'UpdatedAt', now);

    // carry over last sent markers
    // const oldLastSentYm = subsRowValues[subsHeaders.get('LastSentYm')];
    // const oldLastSentAt = subsRowValues[subsHeaders.get('LastSentAt')];
    // setByHeader_(newRow, subsHeaders, 'LastSentYm', oldLastSentYm ?? '');
    // setByHeader_(newRow, subsHeaders, 'LastSentAt', oldLastSentAt ?? '');

    shSubs.appendRow(newRow);
    created++;

    appendLog_({
      timestamp: now, runId, email, action: 'SYNC', reason: 'created (versioned)',
      portfolios, monthKey: getMonthKey_(now), details: `scheduleMode=${scheduleMode}`
    });
  }

  return { created, updated };
}

/***************
 * EVAL: Determine due users and log (DRY_RUN only for now)
 ***************/
function evaluateDueAndLog_(runId, now, profiles) {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  const shSubs = ss.getSheetByName(SHEET_SUBSCRIBERS);
  if (!shSubs) throw new Error(`Missing sheet: ${SHEET_SUBSCRIBERS}`);

  const values = shSubs.getDataRange().getValues();
  if (values.length < 2) {
    appendLog_({
      timestamp: now, runId, email: '', action: 'EVAL', reason: 'no subscribers',
      portfolios: '', monthKey: getMonthKey_(now), details: 'Subscribers has no data rows'
    });
    return { due: 0, skipped: 0, errors: 0 };
  }

  const headers = buildHeaderIndex_(values[0]);
  const monthKey = getMonthKey_(now);

  // Required Subscribers headers for evaluation + state updates
  const reqEval = ['Email', 'Active', 'ScheduleMode', 'Portfolios', 'LastSentYm', 'LastSentAt', 'LastSentFingerprint'];
  for (const h of reqEval) {
    if (headers.get(h) === undefined) {
      throw new Error(`Subscribers missing required header: ${h}`);
    }
  }

  // Ile razy dany email już dostał maila w tym miesiącu + jakie fingerprinty już wysłano
  const sentCountByEmail = new Map();   // email -> number
  const sentFpByEmail = new Map();      // email -> Set<string>

  for (let i = 1; i < values.length; i++) {
    const e = String(values[i][headers.get('Email')] || '').trim().toLowerCase();
    if (!e) continue;

    const ym = normalizeMonthKeyCell_(values[i][headers.get('LastSentYm')]);
    if (ym !== monthKey) continue;

    // Uznajemy jako "wysłane" tylko jeśli jest LastSentAt
    const lastAt = values[i][headers.get('LastSentAt')];
    const hasLastAt = Boolean(toDateOrNull_(lastAt) || String(lastAt ?? '').trim());
    if (!hasLastAt) continue;

    // count
    sentCountByEmail.set(e, (sentCountByEmail.get(e) || 0) + 1);

    // fp set (tylko dla realnie wysłanych)
    const fp = String(values[i][headers.get('LastSentFingerprint')] || '').trim();
    if (fp) {
      if (!sentFpByEmail.has(e)) sentFpByEmail.set(e, new Set());
      sentFpByEmail.get(e).add(fp);
    }
  }

  let due = 0;
  let skipped = 0;
  let errors = 0;

  const today = stripTime_(now);

  for (let r = 1; r < values.length; r++) {
    const row = values[r];
    const email = String(row[headers.get('Email')] || '').trim().toLowerCase();
    if (!email) continue;

    const active = toBool_(row[headers.get('Active')]);
    if (!active) {
      skipped++;
      appendLog_({ timestamp: now, runId, email, action: 'SKIPPED', reason: 'inactive',
        portfolios: String(row[headers.get('Portfolios')] || ''), monthKey, details: '' });
      continue;
    }

    try {
      const scheduleMode = String(row[headers.get('ScheduleMode')] || '').trim();
      const portfolios = String(row[headers.get('Portfolios')] || '').trim();

      const isDue = isDueToday_(row, headers, today);
      if (!isDue) {
        skipped++;
        appendLog_({ timestamp: now, runId, email, action: 'SKIPPED', reason: 'not due today',
          portfolios, monthKey, details: '' });
        continue;
      }

      const currentFp = buildSendFingerprint_(row, headers);

      // Bezpiecznik: max N wysyłek / miesiąc / email
      const alreadySentCount = sentCountByEmail.get(email) || 0;
      if (alreadySentCount >= MAX_SENDS_PER_MONTH_PER_EMAIL) {
        skipped++;
        appendLog_({
          timestamp: now, runId, email, action: 'SKIPPED', reason: 'monthly cap reached',
          portfolios, monthKey, details: `cap=${MAX_SENDS_PER_MONTH_PER_EMAIL}`
        });
        continue;
      }

      // Antyduplikacja: blokuj tylko jeśli w tym miesiącu wysłaliśmy JUŻ dla tej samej konfiguracji
      const sentSet = sentFpByEmail.get(email);
      if (currentFp && sentSet && sentSet.has(currentFp)) {
        skipped++;
        appendLog_({
          timestamp: now, runId, email, action: 'SKIPPED', reason: 'already sent this month (same config)',
          portfolios, monthKey, details: ''
        });
        continue;
      }

      if (!isAllowedBySendAllowlist_(email)) {
        skipped++;
        appendLog_({
          timestamp: now,
          runId,
          email,
          action: 'SKIPPED',
          reason: 'not in allowlist',
          portfolios,
          monthKey,
          details: 'SEND_ALLOWLIST is set and this email is not included'
        });
        continue;
      }

      // Due today
      due++;

      const action = DRY_RUN ? 'DRY_RUN_SENT' : 'DUE';
      appendLog_({ timestamp: now, runId, email, action, reason: 'due today',
        portfolios, monthKey, details: `mode=${scheduleMode}` });

      // Build and log email preview (still DRY_RUN - no sending here)
      try {
        const msg = buildEmailMessage_(email, portfolios, profiles);
        appendLog_({
          timestamp: now,
          runId,
          email,
          action: 'EMAIL_PREVIEW',
          reason: DRY_RUN ? 'dry run preview' : 'send preview',
          portfolios,
          monthKey,
          details: msg.preview
        });
      } catch (e) {
        appendLog_({
          timestamp: now,
          runId,
          email,
          action: 'EMAIL_PREVIEW_ERROR',
          reason: 'preview failed',
          portfolios,
          monthKey,
          details: String(e)
        });
      }

      // Send email (only when DRY_RUN=false)
      if (!DRY_RUN) {
        try {
          const msg = buildEmailMessage_(email, portfolios, profiles);
          sendEmail_(email, msg);

          appendLog_({
            timestamp: now,
            runId,
            email,
            action: 'SENT',
            reason: 'email sent',
            portfolios,
            monthKey,
            details: `subject=${msg.subject}`
          });
        } catch (e) {
          errors++;
          appendLog_({
            timestamp: now,
            runId,
            email,
            action: 'ERROR',
            reason: 'send failed',
            portfolios,
            monthKey,
            details: String(e)
          });
          continue; // do not mark LastSent if send failed
        }
      }

      // Update state (optional in DRY_RUN)
      if (!DRY_RUN || DRY_RUN_UPDATES_LAST_SENT) {
        const sheetRow = r + 1;
        shSubs.getRange(sheetRow, headers.get('LastSentYm') + 1).setValue("'" + monthKey);
        shSubs.getRange(sheetRow, headers.get('LastSentAt') + 1).setValue(now);
        shSubs.getRange(sheetRow, headers.get('LastSentFingerprint') + 1).setValue(currentFp);

        // ważne: aktualizuj licznik w RAM, żeby w tym samym runie nie wysłać drugi raz
        sentCountByEmail.set(email, (sentCountByEmail.get(email) || 0) + 1);

        if (currentFp) {
          if (!sentFpByEmail.has(email)) sentFpByEmail.set(email, new Set());
          sentFpByEmail.get(email).add(currentFp);
        }
      }

    } catch (e) {
      errors++;
      appendLog_({ timestamp: now, runId, email, action: 'ERROR', reason: 'eval failed',
        portfolios: String(row[headers.get('Portfolios')] || ''), monthKey, details: String(e) });
    }
  }

  return { due, skipped, errors };
}

/***************
 * DUE DATE RULES
 ***************/
function isDueToday_(row, headers, today) {
  const mode = String(row[headers.get('ScheduleMode')] || '').trim();

  if (mode === 'DAY_OF_MONTH') {
    const day = parseOptionalInt_(row[headers.get('DayOfMonth')]);
    if (!day) return false;

    const lastDay = getLastDayOfMonth_(today.getFullYear(), today.getMonth());
    const dueDay = Math.min(day, lastDay);
    return today.getDate() === dueDay;
  }

  if (mode === 'WEEKDAY_RULE') {
    const weekRaw = String(row[headers.get('WeekOfMonth')] || '').trim();
    const dowRaw = String(row[headers.get('DayOfWeek')] || '').trim();
    const offset = parseOptionalInt_(row[headers.get('OffsetDays')]) ?? 0;

    if (!weekRaw || !dowRaw) return false;

    const weekdayJs = mapDowCodeToJs_(dowRaw); // 0=Sun..6=Sat
    const base = computeWeekdayRuleDate_(today.getFullYear(), today.getMonth(), weekRaw, weekdayJs);
    if (!base) return false;

    const dueDate = addDays_(base, offset);
    return sameDate_(today, dueDate);
  }

  // Unknown
  return false;
}

function computeWeekdayRuleDate_(year, monthIndex0, weekRaw, weekdayJs) {
  if (weekRaw === 'LAST') {
    return getLastWeekdayOfMonth_(year, monthIndex0, weekdayJs);
  }

  const n = parseInt(weekRaw, 10);
  if (!n || n < 1 || n > 4) return null;
  return getNthWeekdayOfMonth_(year, monthIndex0, weekdayJs, n);
}

function getNthWeekdayOfMonth_(year, monthIndex0, weekdayJs, n) {
  const first = new Date(year, monthIndex0, 1);
  const firstDow = first.getDay();
  const delta = (weekdayJs - firstDow + 7) % 7;
  const day = 1 + delta + (n - 1) * 7;
  const candidate = new Date(year, monthIndex0, day);
  if (candidate.getMonth() !== monthIndex0) return null;
  return stripTime_(candidate);
}

function getLastWeekdayOfMonth_(year, monthIndex0, weekdayJs) {
  const lastDay = getLastDayOfMonth_(year, monthIndex0);
  let d = new Date(year, monthIndex0, lastDay);
  d = stripTime_(d);
  while (d.getDay() !== weekdayJs) {
    d = addDays_(d, -1);
  }
  return d;
}

/***************
 * LOGGING
 ***************/
function appendLog_(entry) {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  const sh = ss.getSheetByName(SHEET_LOGS);
  if (!sh) throw new Error(`Missing sheet: ${SHEET_LOGS}`);

  // Ensure header exists
  const header = sh.getRange(1, 1, 1, sh.getLastColumn()).getValues()[0];
  const idx = buildHeaderIndex_(header);

  const row = new Array(idx.size).fill('');
  setByHeader_(row, idx, 'Timestamp', entry.timestamp);
  setByHeader_(row, idx, 'RunId', entry.runId);
  setByHeader_(row, idx, 'Email', entry.email);
  setByHeader_(row, idx, 'Action', entry.action);
  setByHeader_(row, idx, 'Reason', entry.reason);
  setByHeader_(row, idx, 'Portfolios', entry.portfolios);
  setByHeader_(row, idx, 'MonthKey', entry.monthKey);
  setByHeader_(row, idx, 'Details', entry.details);

  sh.appendRow(row);
}

/***************
 * HELPERS: header mapping and normalization
 ***************/
function buildHeaderIndex_(headerRow) {
  const map = new Map();
  for (let i = 0; i < headerRow.length; i++) {
    const key = normalizeHeader_(headerRow[i]);
    if (key) map.set(key, i);
  }
  return map;
}

function normalizeHeader_(h) {
  return String(h || '')
    .replace(/\s+/g, ' ')
    .trim();
}

function getResp_(row, respHeaders, headerName) {
  const idx = respHeaders.get(normalizeHeader_(headerName));
  if (idx === undefined) return '';
  const v = row[idx];
  return String(v ?? '').trim();
}

function getRespRaw_(row, respHeaders, headerName) {
  const idx = respHeaders.get(normalizeHeader_(headerName));
  if (idx === undefined) return '';
  return row[idx];
}

function setByHeader_(row, headerIndex, headerName, value) {
  const idx = headerIndex.get(normalizeHeader_(headerName));
  if (idx === undefined) return;
  row[idx] = value;
}

/***************
 * HELPERS: mapping from form text
 ***************/
function mapScheduleMode_(text) {
  const t = String(text || '').toLowerCase();
  if (t.includes('konkretny dzień')) return 'DAY_OF_MONTH';
  if (t.includes('reguła tygodniowa')) return 'WEEKDAY_RULE';
  // fallback: try detect
  if (t.includes('dzień miesiąca')) return 'DAY_OF_MONTH';
  return '';
}

function mapOffsetDays_(text) {
  const t = String(text || '').toLowerCase();
  if (!t) return '';
  if (t.includes('wybranym dniu') || t.includes('tego samego')) return 0;
  if (t.includes('1') && t.includes('wcześniej')) return -1;
  if (t.includes('2') && t.includes('wcześniej')) return -2;
  // fallback parse number
  const m = t.match(/(\d+)/);
  if (m && t.includes('wcześniej')) return -parseInt(m[1], 10);
  return 0;
}

function mapWeekOfMonth_(text) {
  const t = String(text || '').toLowerCase().trim();
  if (!t) return '';
  if (t.includes('ostatni')) return 'LAST';
  const m = t.match(/(\d+)/);
  if (m) return String(parseInt(m[1], 10));
  return '';
}

function mapDayOfWeek_(text) {
  const t = String(text || '').toLowerCase().trim();
  if (!t) return '';
  if (t === 'poniedziałek') return 'MON';
  if (t === 'wtorek') return 'TUE';
  if (t === 'środa' || t === 'sroda') return 'WED';
  if (t === 'czwartek') return 'THU';
  if (t === 'piątek' || t === 'piatek') return 'FRI';
  if (t === 'sobota') return 'SAT';
  if (t === 'niedziela') return 'SUN';
  return '';
}

function mapDowCodeToJs_(code) {
  // JS Date.getDay(): 0=Sun..6=Sat
  switch (String(code || '').toUpperCase()) {
    case 'SUN': return 0;
    case 'MON': return 1;
    case 'TUE': return 2;
    case 'WED': return 3;
    case 'THU': return 4;
    case 'FRI': return 5;
    case 'SAT': return 6;
    default: return null;
  }
}

/***************
 * DATE HELPERS
 ***************/
function getMonthKey_(d) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  return `${y}-${m}`;
}

function stripTime_(d) {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

function sameDate_(a, b) {
  return a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate();
}

function addDays_(d, days) {
  const c = new Date(d.getTime());
  c.setDate(c.getDate() + days);
  return stripTime_(c);
}

function getLastDayOfMonth_(year, monthIndex0) {
  return new Date(year, monthIndex0 + 1, 0).getDate();
}

function parseOptionalInt_(v) {
  if (v === null || v === undefined) return null;
  let s = String(v).trim();
  if (s.startsWith("'")) s = s.slice(1).trim();
  const n = parseInt(s, 10);
  return Number.isFinite(n) ? n : null;
}

function toBool_(v) {
  if (typeof v === 'boolean') return v;
  const s = String(v || '').trim().toLowerCase();
  if (s === 'true' || s === '1' || s === 'tak' || s === 'yes') return true;
  if (s === 'false' || s === '0' || s === 'nie' || s === 'no') return false;
  // empty -> false
  return false;
}

function debugListSheets() {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  Logger.log('Spreadsheet name: ' + ss.getName());
  Logger.log('Sheets:');
  ss.getSheets().forEach(sh => Logger.log('[' + sh.getName() + ']'));
}

function normalizeMonthKeyCell_(v) {
  if (v === null || v === undefined || v === '') return '';

  // If Sheets stores it as a Date (common when value looks like "2026-01")
  if (v instanceof Date) {
    return getMonthKey_(v);
  }

  let s = String(v).trim();
  if (s.startsWith("'")) s = s.slice(1).trim();
  if (!s) return '';

  // YYYY-MM
  let m = s.match(/^(\d{4})-(\d{2})$/);
  if (m) return `${m[1]}-${m[2]}`;

  // YYYY-MM-DD or longer
  m = s.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (m) return `${m[1]}-${m[2]}`;

  // Try parse as date string
  const d = new Date(s);
  if (!isNaN(d.getTime())) return getMonthKey_(d);

  // Fallback
  return s;
}

function loadProfiles_() {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  const sh = ss.getSheetByName(SHEET_PROFILES);
  if (!sh) throw new Error(`Missing sheet: ${SHEET_PROFILES}`);

  const values = sh.getDataRange().getValues();
  if (values.length < 2) return new Map();

  const headers = buildHeaderIndex_(values[0]);
  const map = new Map();

  for (let r = 1; r < values.length; r++) {
    const row = values[r];
    const code = String(row[headers.get('ProfileCode')] || '').trim();
    const name = String(row[headers.get('DisplayName')] || '').trim();
    const url = String(row[headers.get('SignalsUrl')] || '').trim();
    const pageUrl = String(row[headers.get('PageUrl')] || '').trim();
    const isActive = toBool_(row[headers.get('IsActive')]);

    if (!code) continue;
    if (!isActive) continue;

    map.set(code, { code, name: name || code, url, pageUrl });
  }

  return map;
}

function buildEmailPreview_(email, portfoliosText, profiles) {
  // Extract codes like GEM_EU/GEM_PL/GEM_US from Subscribers.Portfolios text
  const codes = extractProfileCodes_(portfoliosText);

  if (codes.length === 0) {
    throw new Error('No recognizable profile codes in Portfolios text.');
  }

  const cache = getRunCache_();
  const lines = [];

  for (const code of codes) {
    const profile = profiles.get(code);
    if (!profile) {
      lines.push(`${code}: brak w Profiles (ProfileCode nie istnieje lub IsActive=FALSE)`);
      continue;
    }
    if (!profile.url) {
      lines.push(`${code}: brak SignalsUrl w Profiles`);
      continue;
    }

    const signals = fetchSignalsJson_(profile.url, cache);
    const latest = selectLatestSignal_(signals);
    const summary = formatSignalSummary_(latest);
    lines.push(`${profile.name}: ${summary}`);
  }

  // Minimalny "podgląd" – wrzucamy w Details (krótko)
  const details = lines.join(' | ');
  return { details };
}

function extractProfileCodes_(portfoliosText) {
  const text = String(portfoliosText || '').trim();
  if (!text) return [];

  // Portfolios by default are comma-separated (from checkbox list)
  const parts = text.split(',').map(p => p.trim()).filter(Boolean);

  const codes = [];
  for (const p of parts) {
    // Example: "GEM EU - ..." -> GEM_EU
    const m = p.match(/^GEM\s+(EU|PL|US)\b/i);
    if (m) {
      codes.push(`GEM_${m[1].toUpperCase()}`);
      continue;
    }
  }

  // unique, stable order
  return Array.from(new Set(codes));
}

function getRunCache_() {
  // simple in-memory cache for this execution
  if (!globalThis.__runCache) globalThis.__runCache = new Map();
  return globalThis.__runCache;
}

function fetchSignalsJson_(url, cache) {
  const key = `signals:${url}`;
  if (cache.has(key)) return cache.get(key);

  const resp = UrlFetchApp.fetch(url, { muteHttpExceptions: true });
  const code = resp.getResponseCode();
  const body = resp.getContentText();

  if (code < 200 || code >= 300) {
    throw new Error(`Fetch failed (${code}) for ${url}`);
  }

  let json;
  try {
    json = JSON.parse(body);
  } catch (e) {
    throw new Error(`Invalid JSON from ${url}: ${e}`);
  }

  cache.set(key, json);
  return json;
}

function selectLatestSignal_(signalsJson) {
  // Expected: array of signals
  if (Array.isArray(signalsJson)) {
    if (signalsJson.length === 0) throw new Error('signals.json is empty array');

    // If newest is already first, this is cheap; otherwise we pick max(date)
    let best = signalsJson[0];
    let bestDate = parseSignalDate_(best);

    for (let i = 1; i < signalsJson.length; i++) {
      const d = parseSignalDate_(signalsJson[i]);
      if (d && (!bestDate || d > bestDate)) {
        best = signalsJson[i];
        bestDate = d;
      }
    }
    return best;
  }

  // If it’s wrapped (future-proof)
  if (signalsJson && Array.isArray(signalsJson.signals)) {
    return selectLatestSignal_(signalsJson.signals);
  }

  throw new Error('Unsupported signals.json format (expected array).');
}

function parseSignalDate_(signal) {
  const s = signal?.date;
  if (!s) return null;
  const d = new Date(String(s));
  return isNaN(d.getTime()) ? null : d;
}

function formatSignalSummary_(signal) {
  // Tolerant formatting – works even if some fields are missing
  const date = signal?.date ?? '?';
  const isRiskOn = signal?.isRiskOn;
  const riskText = (isRiskOn === true) ? 'RISK-ON' : (isRiskOn === false ? 'RISK-OFF' : 'UNKNOWN');

  const allocations = Array.isArray(signal?.allocations) ? signal.allocations : [];
  const allocText = allocations
    .map(a => {
      const t = a.ticker ?? a.symbol ?? a.id ?? '?';
      const w = (a.weight !== undefined && a.weight !== null) ? `${Math.round(a.weight * 100)}%` : '';
      return `${t}${w ? ' ' + w : ''}`;
    })
    .join(', ');

  return `${date} ${riskText}${allocText ? ' | ' + allocText : ''}`;
}

function buildEmailMessage_(email, portfoliosText, profiles) {
  const codes = extractProfileCodes_(portfoliosText);
  if (codes.length === 0) throw new Error('No recognizable profile codes in Portfolios text.');

  const cache = getRunCache_();

  const sections = [];
  const textSections = [];

  // dla tematu – bierzemy najnowszą datę sygnału z wszystkich profili
  let bestSignalDate = null;

  for (const code of codes) {
    const profile = profiles.get(code);
    if (!profile) {
      sections.push(renderHtmlSection_(code, null, `Brak wpisu w Profiles (ProfileCode=${code}).`));
      textSections.push(`${code}: brak wpisu w Profiles`);
      continue;
    }
    if (!profile.url) {
      sections.push(renderHtmlSection_(profile.name, null, 'Brak SignalsUrl w Profiles.'));
      textSections.push(`${profile.name}: brak SignalsUrl`);
      continue;
    }

    const signals = fetchSignalsJson_(profile.url, cache);
    const latest = selectLatestSignal_(signals);

    const d = parseSignalDate_(latest);
    if (d && (!bestSignalDate || d > bestSignalDate)) bestSignalDate = d;

    sections.push(renderHtmlSection_(profile.name, latest, profile.url, profile.pageUrl));
    textSections.push(renderTextSection_(profile.name, latest, profile.url, profile.pageUrl));
  }

  const monthKey = bestSignalDate ? getMonthKey_(bestSignalDate) : getMonthKey_(new Date());

  const subject = `Strategy Notifier | sygnały ${monthKey}`;

  const headerHtml = `
    <div style="background:#f6f7fb;padding:24px 0">
      <div style="max-width:640px;margin:0 auto;padding:0 16px;font-family:Arial,Helvetica,sans-serif">
        <div style="background:#ffffff;border:1px solid #e6e8f0;border-radius:12px;box-shadow:0 1px 2px rgba(0,0,0,0.04);overflow:hidden">
          <div style="padding:18px 20px;border-bottom:1px solid #eef0f6">
            <div style="font-size:14px;color:#6b7280;margin-bottom:6px">Strategy Notifier</div>
            <div style="font-size:20px;font-weight:700;color:#111827;line-height:1.2">Sygnały miesięczne</div>
            <div style="margin-top:10px;font-size:13px;color:#374151;line-height:1.45">
              Poniżej znajdziesz sygnały dla wybranych portfeli. Aby zobaczyć szczegóły i historię, kliknij <strong>"Zobacz na stronie"</strong>.
            </div>
          </div>
          <div style="padding:14px 20px">
  `;

  const footerHtml = `
          </div>
          <div style="padding:14px 20px;border-top:1px solid #eef0f6;background:#fbfbfe">
            <div style="font-size:12px;color:#6b7280;line-height:1.45">
              Wypisanie: ${escapeHtml_(getUnsubscribeContact_())}<br/>
              Polityka prywatności: ${escapeHtml_(getPrivacyPolicyUrl_())}
            </div>
          </div>
        </div>
        <div style="max-width:640px;margin:0 auto;padding:10px 16px 0;font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#9ca3af;line-height:1.35">
          Wiadomość generowana automatycznie na podstawie publicznych danych strategii. Nie stanowi porady inwestycyjnej.
        </div>
      </div>
    </div>
  `;

  const htmlBody = headerHtml + sections.join('') + footerHtml;

  const textBody =
`Oto Twoje comiesięczne sygnały dla wybranych portfoliów.

${textSections.join('\n\n')}

---
Wypisanie: ${getUnsubscribeContact_()}
Polityka prywatności: ${getPrivacyPolicyUrl_()}
`;

  // krótszy "preview" do logów
  const preview = textSections.map(s => s.replace(/\s+/g, ' ').trim()).join(' | ');

  return { subject, htmlBody, textBody, preview };
}

function renderHtmlSection_(displayName, signal, sourceUrlOrMessage, pageUrl) {
  const title = escapeHtml_(String(displayName || 'Portfolio'));

  if (!signal) {
    return `
      <div style="padding:14px 0;border-bottom:1px solid #f0f2f8">
        <div style="font-size:16px;font-weight:700;color:#111827">${title}</div>
        <div style="margin-top:6px;font-size:13px;color:#b91c1c">${escapeHtml_(String(sourceUrlOrMessage || 'Brak danych'))}</div>
      </div>
    `;
  }

  const dateRaw = String(signal.date ?? '?');
  const date = escapeHtml_(dateRaw);

  const isRiskOn = signal.isRiskOn === true;
  // const riskLabel = isRiskOn ? 'RISK-ON' : 'RISK-OFF';
  // const riskBg = isRiskOn ? '#ecfdf5' : '#fff1f2';
  // const riskFg = isRiskOn ? '#065f46' : '#9f1239';
  // const riskBorder = isRiskOn ? '#a7f3d0' : '#fecdd3';
  const modeText = isRiskOn ? 'Tryb: ofensywny' : 'Tryb: defensywny';

  const allocations = Array.isArray(signal.allocations) ? signal.allocations : [];

  const allocRows = allocations.length
    ? allocations.map(a => {
        const rawTicker = String(a.ticker ?? a.symbol ?? a.id ?? '?');
        const n = String(a.name ?? '').trim();
        const label = n ? `${n} (${rawTicker})` : rawTicker;
        const safeLabel = preventAutolinkHtml_(escapeHtml_(label));

        const w = (a.weight !== undefined && a.weight !== null) ? `${Math.round(a.weight * 100)}%` : '';
        const safeW = escapeHtml_(w);

        return `
          <tr>
            <td style="padding:8px 0;font-size:13px;color:#111827">${safeLabel}</td>
            <td style="padding:8px 0;font-size:13px;color:#111827;text-align:right;white-space:nowrap">${safeW}</td>
          </tr>
        `;
      }).join('')
    : `
      <tr>
        <td style="padding:8px 0;font-size:13px;color:#6b7280" colspan="2">Brak alokacji.</td>
      </tr>
    `;

  const tidy = tidyCommentForEmail_(signal.comment);
  const comment = tidy ? `<div style="margin-top:10px;font-size:13px;color:#374151;line-height:1.45">${escapeHtml_(tidy)}</div>` : '';

  const pageLink = pageUrl && String(pageUrl).startsWith('http')
    ? `<a href="${escapeHtml_(pageUrl)}" style="display:inline-block;margin-top:12px;padding:8px 12px;border-radius:10px;background:#111827;color:#ffffff;text-decoration:none;font-size:12px;font-weight:700">Zobacz na stronie</a>`
    : '';

  return `
    <div style="padding:14px 0;border-bottom:1px solid #f0f2f8">
      <div style="display:flex;align-items:center;justify-content:space-between;gap:12px">
        <div style="font-size:16px;font-weight:800;color:#111827">${title}</div>
      </div>

      <div style="margin-top:6px;font-size:13px;color:#6b7280">
        ${escapeHtml_(modeText)} • Data sygnału: <strong style="color:#111827">${date}</strong>
      </div>

      <div style="margin-top:10px;border:1px solid #eef0f6;border-radius:10px;padding:10px 12px">
        <div style="font-size:12px;color:#6b7280;font-weight:700;text-transform:uppercase;letter-spacing:.04em">Alokacja</div>
        <table style="width:100%;border-collapse:collapse;margin-top:6px">
          ${allocRows}
        </table>
      </div>

      ${comment}
      ${pageLink}
    </div>
  `;
}

function renderTextSection_(displayName, signal, sourceUrl, pageUrl) {
  const name = String(displayName || 'Portfolio');

  if (!signal) return `${name}: brak danych`;

  const date = String(signal.date ?? '?');
  const risk = (signal.isRiskOn === true) ? 'RISK-ON' : (signal.isRiskOn === false ? 'RISK-OFF' : 'UNKNOWN');
  const modeText = (signal.isRiskOn === true) ? 'Tryb: ofensywny' : 'Tryb: defensywny';

  const allocations = Array.isArray(signal.allocations) ? signal.allocations : [];
  const allocText = allocations.length
    ? allocations.map(a => {
        const rawTicker = String(a.ticker ?? a.symbol ?? a.id ?? '?');
        const n = String(a.name ?? '').trim();
        const label = n ? `${n} (${rawTicker})` : rawTicker;
        const safeLabel = preventAutolinkText_(label);

        const w = (a.weight !== undefined && a.weight !== null) ? `${Math.round(a.weight * 100)}%` : '';
        return `${safeLabel}${w ? ' ' + w : ''}`;
      }).join(', ')
    : '(brak alokacji)';

  const tidy = tidyCommentForEmail_(signal.comment);
  const comment = tidy ? `\nKomentarz: ${tidy}` : '';

  const page = (pageUrl && String(pageUrl).startsWith('http')) ? `\nStrona: ${pageUrl}` : '';

  return `${name}: ${date} ${modeText}\nAlokacje: ${allocText}${comment}${page}`;
}

function escapeHtml_(s) {
  return String(s ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function getScriptProperty_(key, fallbackValue) {
  const props = PropertiesService.getScriptProperties();
  const v = props.getProperty(key);
  if (v === null || v === undefined) return fallbackValue;
  const s = String(v).trim();
  return s ? s : fallbackValue;
}

function getUnsubscribeContact_() {
  return getScriptProperty_(PROP_UNSUBSCRIBE_CONTACT, '[UNSUBSCRIBE_CONTACT_NOT_SET]');
}

function getPrivacyPolicyUrl_() {
  return getScriptProperty_(PROP_PRIVACY_POLICY_URL, '[PRIVACY_POLICY_URL_NOT_SET]');
}

function getSendAllowlist_() {
  const raw = getScriptProperty_(PROP_SEND_ALLOWLIST, '');
  if (!raw) return [];

  // Split by commas/semicolons/newlines/spaces, keep only email-like tokens
  return raw
    .split(/[,;\n\r\t ]+/)
    .map(x => x.trim().toLowerCase())
    .filter(x => x && x.includes('@'));
}

function isAllowedBySendAllowlist_(email) {
  const list = getSendAllowlist_();
  if (list.length === 0) return true; // allowlist disabled => allow all
  const e = String(email || '').trim().toLowerCase();
  return list.includes(e);
}

function deduplicateSubscribers_(runId, now) {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  const sh = ss.getSheetByName(SHEET_SUBSCRIBERS);
  if (!sh) throw new Error(`Missing sheet: ${SHEET_SUBSCRIBERS}`);

  const values = sh.getDataRange().getValues();
  if (values.length < 2) return { deactivated: 0 };

  const headers = buildHeaderIndex_(values[0]);

  const idxEmail = headers.get('Email');
  const idxActive = headers.get('Active');
  const idxUpdatedAt = headers.get('UpdatedAt');
  const idxCreatedAt = headers.get('CreatedAt');
  const idxFormTs = headers.get('FormTimestamp'); // może być undefined, ale mamy kolumnę

  if (idxEmail === undefined || idxActive === undefined) {
    throw new Error('Subscribers sheet missing required headers: Email/Active');
  }

  // group by fingerprint (already includes email in your version)
  // 1) Dedup po EMAIL: chcemy maks. 1 aktywny rekord na email
  const byEmail = new Map(); // email -> items[]

  for (let r = 1; r < values.length; r++) {
    const row = values[r];

    const email = String(row[idxEmail] || '').trim().toLowerCase();
    if (!email) continue;

    const isActive = toBool_(row[idxActive]);
    if (!isActive) continue;

    const formTsMs = (idxFormTs !== undefined) ? (toDateOrNull_(row[idxFormTs])?.getTime() ?? 0) : 0;
    const updatedMs = (idxUpdatedAt !== undefined) ? (toDateOrNull_(row[idxUpdatedAt])?.getTime() ?? 0) : 0;
    const createdMs = (idxCreatedAt !== undefined) ? (toDateOrNull_(row[idxCreatedAt])?.getTime() ?? 0) : 0;

    // preferujemy FormTimestamp, potem UpdatedAt, potem CreatedAt
    const score = formTsMs || updatedMs || createdMs || 0;

    if (!byEmail.has(email)) byEmail.set(email, []);
    byEmail.get(email).push({ sheetRow: r + 1, score, email });
  }

  let deactivated = 0;

  for (const [email, items] of byEmail.entries()) {
    if (items.length <= 1) continue;

    items.sort((a, b) => b.score - a.score);
    const keep = items[0];

    for (let i = 1; i < items.length; i++) {
      const it = items[i];

      sh.getRange(it.sheetRow, idxActive + 1).setValue(false);
      if (idxUpdatedAt !== undefined) {
        sh.getRange(it.sheetRow, idxUpdatedAt + 1).setValue(now);
      }

      deactivated++;

      appendLog_({
        timestamp: now,
        runId,
        email: it.email,
        action: 'DEDUP',
        reason: 'deactivated duplicate (active, same email)',
        portfolios: '',
        monthKey: getMonthKey_(now),
        details: `keptRow=${keep.sheetRow}`
      });
    }
  }

  return { deactivated };
}

function buildSubscriberFingerprint_(row, headers) {
  // DEDUP only within the same email (safe for multiple subscribers)
  const email = String(row[headers.get('Email')] || '').trim().toLowerCase();

  const mode = String(row[headers.get('ScheduleMode')] || '').trim();
  const dom = String(row[headers.get('DayOfMonth')] || '').trim();
  const wom = String(row[headers.get('WeekOfMonth')] || '').trim();
  const dow = String(row[headers.get('DayOfWeek')] || '').trim();
  const off = String(row[headers.get('OffsetDays')] || '').trim();
  const portfolios = normalizePortfoliosText_(String(row[headers.get('Portfolios')] || ''));

  if (!email || !mode) return '';

  return [email, mode, dom, wom, dow, off, portfolios].join('|');
}

function normalizePortfoliosText_(s) {
  // Normalize to reduce false differences (spacing/order in checkbox output)
  const parts = String(s || '')
    .split(',')
    .map(x => x.trim())
    .filter(Boolean)
    .sort((a, b) => a.localeCompare(b));

  return parts.join(', ');
}

function toDateOrNull_(v) {
  if (v instanceof Date) return v;

  // epoch ms as number
  if (typeof v === 'number' && Number.isFinite(v)) {
    const d = new Date(v);
    return isNaN(d.getTime()) ? null : d;
  }

  let s = String(v || '').trim();
  if (!s) return null;

  // epoch ms as string (13 digits)
  if (/^\d{13}$/.test(s)) {
    const n = Number(s);
    const d = new Date(n);
    return isNaN(d.getTime()) ? null : d;
  }

  const d = new Date(s);
  return isNaN(d.getTime()) ? null : d;
}

function sendEmail_(toEmail, message) {
  MailApp.sendEmail({
    to: toEmail,
    name: SENDER_NAME,
    subject: message.subject,
    htmlBody: message.htmlBody,
    body: message.textBody
  });
}

function preventAutolinkHtml_(s) {
  // Break patterns like VEU.US, ETFBW20TR.PL without breaking decimals (52.40)
  return String(s ?? '').replace(/\.(?=[A-Za-z])/g, '&#8203;.');
}

function preventAutolinkText_(s) {
  return String(s ?? '').replace(/\.(?=[A-Za-z])/g, '\u200B.');
}

function tidyCommentForEmail_(comment) {
  const s = String(comment || '').trim();
  if (!s) return '';
  // Usuń wiodący ticker: "ETFBW20TR.PL " / "VEU.US "
  return s.replace(/^[A-Z0-9_]+\.[A-Z]{2}\s+/i, '');
}

function buildSendFingerprint_(row, headers) {
  const mode = String(row[headers.get('ScheduleMode')] || '').trim();
  const dom = String(row[headers.get('DayOfMonth')] || '').trim();
  const wom = String(row[headers.get('WeekOfMonth')] || '').trim();
  const dow = String(row[headers.get('DayOfWeek')] || '').trim();
  const off = String(row[headers.get('OffsetDays')] || '').trim();
  const portfolios = normalizePortfoliosText_(String(row[headers.get('Portfolios')] || ''));

  if (!mode) return '';
  return [mode, dom, wom, dow, off, portfolios].join('|');
}
