(() => {
  const LOCAL_STORAGE_KEY = 'gem-profile';
  const LANG_STORAGE_KEY = 'gem-lang';
  const I18N_PATH = './i18n/';
  const SUPPORTED_LANGS = ['en', 'pl'];
  const LIVE_MANIFEST_PATH = '/dist/gem/profiles.json';
  const DEMO_MANIFEST_PATH = './demo/profiles.json';
  const HISTORY_DEFAULT_COUNT = 12;

  const elements = {
    profileSelect: document.getElementById('profile-select'),
    languageSelect: document.getElementById('language-select'),
    statusLine: document.getElementById('status-line'),
    statusText: document.getElementById('status-text'),
    statusSource: document.getElementById('status-source'),
    freshness: document.getElementById('freshness'),
    freshnessGenerated: document.getElementById('freshness-generated'),
    freshnessLoaded: document.getElementById('freshness-loaded'),
    errorPanel: document.getElementById('error-panel'),
    errorMessage: document.getElementById('error-message'),
    orderWarning: document.getElementById('order-warning'),
    currentCard: document.getElementById('current-card'),
    historyCard: document.getElementById('history-card'),
    currentContent: document.getElementById('current-content'),
    commentContainer: document.getElementById('comment-container'),
    modeChip: document.getElementById('mode-chip'),
    currentDate: document.getElementById('current-date'),
    currentProfileLabel: document.getElementById('current-profile-label'),
    historyBody: document.getElementById('history-body'),
    historyCount: document.getElementById('history-count'),
    historyToggle: document.getElementById('history-toggle'),
    refreshButton: document.getElementById('refresh-button'),
    commandText: document.getElementById('command-text')
  };

  let translations = {};
  let fallbackTranslations = {};
  let currentLang = 'en';
  let manifestUrl = null;
  let profiles = [];
  let currentProfileId = null;
  let cacheBuster = 0;
  let source = 'live';
  let manifestState = { ok: false, source: 'live', url: null, lastModified: '', loadedAt: null };
  let lastProfile = null;
  let lastSignals = null;
  let lastSignalsMeta = null;
  let lastError = null;
  let showAllHistory = false;

  document.addEventListener('DOMContentLoaded', () => {
    bootstrap().catch((err) => showFatalError(err instanceof Error ? err.message : String(err)));
  });

  async function bootstrap() {
    await setupLanguage();
    applyStaticTranslations();
    clearFreshness();
    await loadAndRender();

    elements.refreshButton.addEventListener('click', async () => {
      cacheBuster = Date.now();
      await loadAndRender(true);
    });

    elements.historyToggle.addEventListener('click', () => {
      if (!lastSignals) return;
      showAllHistory = !showAllHistory;
      renderHistory(lastSignals);
    });
  }

  async function setupLanguage() {
    const preferred = resolveLanguage();
    await loadTranslations(preferred);
    currentLang = preferred;
    elements.languageSelect.value = currentLang;

    elements.languageSelect.addEventListener('change', async (event) => {
      const lang = event.target.value;
      await loadTranslations(lang);
      currentLang = lang;
      persistLanguage(lang);
      applyStaticTranslations();
      rerenderFromCache();
    });
  }

  async function loadTranslations(lang) {
    const chosen = normalizeLang(lang);
    const fallbackLang = 'en';

    fallbackTranslations = await fetchTranslations(fallbackLang);
    translations = chosen === fallbackLang ? fallbackTranslations : await fetchTranslations(chosen, fallbackTranslations);
  }

  async function fetchTranslations(lang, fallback = {}) {
    try {
      const response = await fetch(`${I18N_PATH}${lang}.json`, { cache: 'no-store' });
      if (!response.ok) throw new Error('Translation fetch failed');
      return await response.json();
    } catch {
      return fallback;
    }
  }

  function resolveLanguage() {
    const stored = window.localStorage.getItem(LANG_STORAGE_KEY);
    if (stored && SUPPORTED_LANGS.includes(stored)) return stored;

    const browserLang = (navigator.language || navigator.userLanguage || '').slice(0, 2).toLowerCase();
    if (SUPPORTED_LANGS.includes(browserLang)) return browserLang;

    return 'en';
  }

  function normalizeLang(lang) {
    if (SUPPORTED_LANGS.includes(lang)) return lang;
    const short = lang.slice(0, 2).toLowerCase();
    return SUPPORTED_LANGS.includes(short) ? short : 'en';
  }

  function applyStaticTranslations() {
    document.querySelectorAll('[data-i18n]').forEach((el) => {
      const key = el.getAttribute('data-i18n');
      el.textContent = t(key);
    });
  }

  function rerenderFromCache() {
    if (!manifestState.ok) {
      showInstruction(t('statusManifestMissing'), true);
      return;
    }

    if (lastError && lastProfile) {
      showError(lastProfile, lastError);
      return;
    }

    if (lastProfile && lastSignals) {
      hideError();
      render(lastProfile, lastSignals, lastSignalsMeta || {});
      updateCommand(lastProfile.id);
      return;
    }

    setStatus(t('statusIdle'), 'info', source === 'live' ? t('pillLive') : t('pillDemo'));
  }

  async function loadAndRender(forceRefresh = false) {
    const manifest = await loadManifest(forceRefresh);
    source = manifest.source;
    if (!manifest.ok || !Array.isArray(manifest.profiles) || manifest.profiles.length === 0) {
      manifestState = { ok: false, source: manifest.source, url: manifest.url?.toString() ?? null, lastModified: '', loadedAt: manifest.loadedAt ?? null };
      lastProfile = null;
      lastSignals = null;
      lastSignalsMeta = null;
      lastError = null;
      clearFreshness();
      showInstruction(t('statusManifestMissing'), true);
      return;
    }

    manifestState = {
      ok: true,
      source: manifest.source,
      url: manifest.url.toString(),
      lastModified: manifest.lastModified || '',
      loadedAt: manifest.loadedAt || Date.now()
    };
    lastError = null;
    profiles = manifest.profiles;
    manifestUrl = manifest.url;
    source = manifest.source;

    setupProfileOptions(profiles);
    const initialProfileId = currentProfileId && profiles.some((p) => p.id === currentProfileId)
      ? currentProfileId
      : getInitialProfileId(profiles);
    elements.profileSelect.value = initialProfileId;

    elements.profileSelect.onchange = async (event) => {
      const selectedId = (event.target.value || '').trim();
      if (!selectedId) return;
      showAllHistory = false;
      await loadProfile(selectedId, forceRefresh);
    };

    showAllHistory = false;
    await loadProfile(initialProfileId, forceRefresh);
  }

  async function loadManifest(forceRefresh) {
    const url = new URL(LIVE_MANIFEST_PATH, window.location.origin);
    if (forceRefresh || cacheBuster) {
      url.searchParams.set('t', (cacheBuster || Date.now()).toString());
    }

    try {
      const response = await fetch(url.toString(), { cache: 'no-store' });
      if (!response.ok) throw new Error('Manifest fetch failed');
      const data = await response.json();
      const profilesList = data.profiles ?? [];

      return {
        ok: true,
        profiles: profilesList,
        url,
        source: 'live',
        lastModified: response.headers.get('Last-Modified') || '',
        loadedAt: Date.now()
      };
    } catch {
      try {
        const demoUrl = new URL(DEMO_MANIFEST_PATH, window.location.href);
        const response = await fetch(demoUrl.toString(), { cache: 'no-store' });
        if (!response.ok) throw new Error('Demo manifest fetch failed');
        const data = await response.json();
        const profilesList = data.profiles ?? [];
        return {
          ok: true,
          profiles: profilesList,
          url: demoUrl,
          source: 'demo',
          lastModified: response.headers.get('Last-Modified') || '',
          loadedAt: Date.now()
        };
      } catch {
        return { ok: false, profiles: [], url: new URL('./', window.location.href), source: 'none', loadedAt: Date.now() };
      }
    }
  }

  async function loadProfile(profileId, forceRefresh) {
    const profile = resolveProfile(profileId);
    currentProfileId = profile.id;
    lastProfile = profile;
    persistProfile(profile.id);
    updateUrl(profile.id);

    hideError();
    setStatus(t('statusLoadingProfile', { profile: profile.title }), 'loading', source === 'live' ? t('pillLive') : t('pillDemo'));
    updateSourcePill(source === 'live');
    toggleCard(elements.currentCard, false);
    toggleCard(elements.historyCard, false);
    showWarning('', false);
    updateCommand(profile.id);

    try {
      const signalsUrl = new URL(profile.signalsPath, manifestUrl);
      if (forceRefresh || cacheBuster) {
        signalsUrl.searchParams.set('t', (cacheBuster || Date.now()).toString());
      }
      const signalsResult = await fetchSignals(signalsUrl.toString());
      const signals = signalsResult.data;
      const signalsMeta = {
        signalsLastModified: signalsResult.lastModified,
        loadedAt: Date.now(),
        firstSignalDate: signals[0]?.date
      };
      lastProfile = profile;
      lastSignals = signals;
      lastSignalsMeta = signalsMeta;
      lastError = null;
      render(profile, signals, signalsMeta);
    } catch (err) {
      showError(profile, err);
    }
  }

  function setupProfileOptions(list) {
    elements.profileSelect.innerHTML = '';
    for (const profile of list) {
      const option = document.createElement('option');
      option.value = profile.id;
      option.textContent = profile.title;
      elements.profileSelect.append(option);
    }
  }

  function resolveProfile(profileId) {
    const normalized = profileId?.trim().toLowerCase();
    const found = profiles.find((p) => p.id.toLowerCase() === normalized);
    return found ?? profiles[0];
  }

  function getInitialProfileId(list) {
    const search = new URLSearchParams(window.location.search);
    const fromQuery = search.get('profile');
    const fromStorage = window.localStorage.getItem(LOCAL_STORAGE_KEY);

    const candidates = [fromQuery, fromStorage, list[0]?.id];
    for (const candidate of candidates) {
      if (!candidate) continue;
      const match = list.find((p) => p.id.toLowerCase() === candidate.toLowerCase());
      if (match) return match.id;
    }

    return list[0].id;
  }

  async function fetchSignals(url) {
    const response = await fetch(url, { cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`.trim());
    }

    const data = await response.json();
    if (!Array.isArray(data)) {
      throw new Error(t('errorInvalidFormat'));
    }

    return { data, lastModified: response.headers.get('Last-Modified') || '' };
  }

  function render(profile, signals, signalsMeta = {}) {
    if (!signals.length) {
      setStatus(t('statusNoSignals', { profile: profile.title }), 'warning', source === 'live' ? t('pillLive') : t('pillDemo'));
      updateSourcePill(source === 'live');
      setFreshness(profile, signalsMeta);
      toggleCard(elements.currentCard, false);
      toggleCard(elements.historyCard, false);
      elements.historyToggle.hidden = true;
      elements.historyCount.textContent = '';
      return;
    }

    const latest = signals[0];
    setStatus(t('statusLoadedProfile', { profile: profile.title }), 'ok', source === 'live' ? t('pillLive') : t('pillDemo'));
    updateSourcePill(source === 'live');
    setFreshness(profile, signalsMeta);

    renderCurrent(profile, latest);
    renderHistory(signals);
    checkOrdering(signals);
  }

  function renderCurrent(profile, signal) {
    toggleCard(elements.currentCard, true);
    elements.currentProfileLabel.textContent = profile.title;
    elements.modeChip.textContent = signal.isRiskOn ? t('modeRiskOn') : t('modeRiskOff');
    elements.modeChip.className = `pill ${signal.isRiskOn ? 'live' : 'off'}`;
    elements.currentDate.textContent = signal.date ? t('signalFor', { date: signal.date }) : '';

    elements.currentContent.innerHTML = '';
    elements.commentContainer.innerHTML = '';

    const allocations = document.createElement('div');
    allocations.className = 'stat';
    const allocLabel = document.createElement('div');
    allocLabel.className = 'label';
    allocLabel.textContent = t('allocationsLabel');
    const list = document.createElement('ul');
    list.className = 'alloc-list';

    (signal.allocations ?? []).forEach((allocation) => {
      const item = document.createElement('li');
      item.className = 'alloc-item';

      const ticker = document.createElement('div');
      ticker.className = 'ticker';
      ticker.textContent = `${formatPercent(allocation.weight)} - ${allocation.ticker}`;

      const name = document.createElement('div');
      name.className = 'muted';
      name.textContent = allocation.name ?? '';

      item.append(ticker, name);
      list.append(item);
    });

    allocations.append(allocLabel, list);
    elements.currentContent.append(allocations);

    const stats = [
      createStat(t('windowLabel'), formatWindowMonths(signal.windowMonths))
    ];

    if (signal.absoluteReturn !== undefined && signal.absoluteReturn !== null) {
      stats.push(createStat(t('absoluteReturnLabel'), formatPercent(signal.absoluteReturn)));
    }

    if (signal.relativeRank !== undefined && signal.relativeRank !== null) {
      stats.push(createStat(t('relativeRankLabel'), `#${signal.relativeRank}`));
    }

    stats.forEach((stat) => elements.currentContent.append(stat));

    if (signal.comment) {
      const commentBlock = document.createElement('div');
      commentBlock.className = 'comment-block';

      const heading = document.createElement('div');
      heading.className = 'label';
      heading.textContent = t('commentLabel');

      const body = document.createElement('p');
      body.className = 'muted';

      if (signal.comment.length > 200) {
        const truncated = `${signal.comment.slice(0, 200).trim()}...`;
        let expanded = false;

        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'link-button';

        const applyText = () => {
          body.textContent = expanded ? signal.comment : truncated;
          toggle.textContent = expanded ? t('showLess') : t('showMore');
          toggle.setAttribute('aria-expanded', expanded.toString());
        };

        applyText();
        toggle.addEventListener('click', () => {
          expanded = !expanded;
          applyText();
        });

        commentBlock.append(heading, body, toggle);
      } else {
        body.textContent = signal.comment;
        commentBlock.append(heading, body);
      }

      elements.commentContainer.append(commentBlock);
    }
  }

  function renderHistory(signals) {
    toggleCard(elements.historyCard, true);
    const visible = showAllHistory ? signals : signals.slice(0, HISTORY_DEFAULT_COUNT);
    elements.historyBody.innerHTML = '';
    visible.forEach((signal) => {
      const row = document.createElement('tr');

      row.append(createCell(formatDate(signal.date)));
      row.append(createModeCell(signal.isRiskOn));
      row.append(createCell(formatWindowMonths(signal.windowMonths)));

      const allocations = createCell(formatAllocations(signal.allocations));
      allocations.style.whiteSpace = 'pre-line';
      row.append(allocations);

      row.append(createCell(formatValueOrDash(signal.absoluteReturn, formatPercent)));

      elements.historyBody.append(row);
    });

    elements.historyCount.textContent = t('historyCount', { current: visible.length, total: signals.length });
    if (signals.length > HISTORY_DEFAULT_COUNT) {
      elements.historyToggle.hidden = false;
      elements.historyToggle.textContent = t(showAllHistory ? 'historyShowLess' : 'historyShowAll');
    } else {
      elements.historyToggle.hidden = true;
    }
  }

  function checkOrdering(signals) {
    if (signals.length < 2) {
      showWarning('', false);
      return;
    }

    const first = parseDate(signals[0].date);
    const second = parseDate(signals[1].date);

    if (!first || !second) {
      showWarning('', false);
      return;
    }

    const isDescending = first >= second;
    showWarning(isDescending ? '' : t('orderWarning'), !isDescending);
  }

  function showWarning(message, visible) {
    if (!visible) {
      elements.orderWarning.hidden = true;
      elements.orderWarning.textContent = '';
      return;
    }

    elements.orderWarning.hidden = false;
    elements.orderWarning.textContent = message;
  }

  function setStatus(message, state, sourceLabel) {
    elements.statusText.textContent = message;
    elements.statusLine.className = `status-line ${state ?? ''}`;
    if (sourceLabel !== undefined) {
      elements.statusSource.textContent = sourceLabel;
      elements.statusSource.className = 'pill';
    }
  }

  function updateSourcePill(isLive) {
    elements.statusSource.className = `pill ${isLive ? 'live' : 'demo'}`;
    elements.statusSource.textContent = isLive ? t('pillLive') : t('pillDemo');
  }

  function showError(profile, error) {
    const message = error instanceof Error ? error.message : String(error);
    setStatus(t('statusErrorProfile', { profile: profile.title }), 'error', source === 'live' ? t('pillLive') : t('pillDemo'));
    updateSourcePill(source === 'live');
    elements.errorMessage.textContent = message;
    elements.errorPanel.hidden = false;
    toggleCard(elements.currentCard, false);
    toggleCard(elements.historyCard, false);
    lastError = error;
    clearFreshness();
  }

  function showInstruction(message, manifestMissing) {
    setStatus(manifestMissing ? message : t('statusIdle'), manifestMissing ? 'error' : 'info', manifestMissing ? '-' : source === 'live' ? t('pillLive') : t('pillDemo'));
    const commandGenerate = t('instructionGenerateAll');
    const commandServe = t('instructionRunCommandRoot');
    elements.errorMessage.textContent = `${message} ${commandGenerate} ${commandServe}`;
    elements.errorPanel.hidden = false;
    toggleCard(elements.currentCard, false);
    toggleCard(elements.historyCard, false);
    clearFreshness();
  }

  function showFatalError(message) {
    setStatus(t('fatalError'), 'error', '-');
    elements.errorMessage.textContent = message;
    elements.errorPanel.hidden = false;
    toggleCard(elements.currentCard, false);
    toggleCard(elements.historyCard, false);
    clearFreshness();
  }

  function hideError() {
    elements.errorPanel.hidden = true;
    elements.errorMessage.textContent = '';
  }

  function createStat(label, value) {
    const container = document.createElement('div');
    container.className = 'stat';

    const labelEl = document.createElement('div');
    labelEl.className = 'label';
    labelEl.textContent = label;

    const valueEl = document.createElement('div');
    valueEl.className = 'value';
    valueEl.textContent = value;

    container.append(labelEl, valueEl);
    return container;
  }

  function createCell(text) {
    const cell = document.createElement('td');
    cell.textContent = text ?? '-';
    return cell;
  }

  function createModeCell(isRiskOn) {
    const cell = document.createElement('td');
    const pill = document.createElement('span');
    pill.className = `mode-chip ${isRiskOn ? 'on' : 'off'}`;
    pill.textContent = isRiskOn ? t('modeRiskOn') : t('modeRiskOff');
    cell.append(pill);
    return cell;
  }

  function formatAllocations(allocations = []) {
    if (!allocations.length) return '-';
    return allocations
      .map((allocation) => `${formatPercent(allocation.weight)} ${allocation.ticker}${allocation.name ? ` (${allocation.name})` : ''}`)
      .join('\n');
  }

  function formatPercent(value) {
    if (value === undefined || value === null || Number.isNaN(Number(value))) return '-';
    const percent = Number(value) * 100;
    const digits = Math.abs(percent) >= 100 ? 0 : 1;
    return `${percent.toFixed(digits)}%`;
  }

  function formatWindowMonths(value) {
    if (value === undefined || value === null) return '-';
    return `${value} ${t('monthsSuffix')}`;
  }

  function formatDate(dateString) {
    const parsed = parseDate(dateString);
    if (!parsed) return dateString || '-';
    return parsed.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
  }

  function formatDateTime(value) {
    if (!value) return '';
    const parsed = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(parsed.getTime())) return '';
    return parsed.toLocaleString();
  }

  function parseDate(dateString) {
    if (!dateString) return null;
    const parsed = new Date(`${dateString}T00:00:00Z`);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  function formatValueOrDash(value, formatter) {
    if (value === undefined || value === null) return '-';
    return formatter(value);
  }

  function toggleCard(card, visible) {
    card.hidden = !visible;
  }

  function setFreshness(profile, signalsMeta = {}) {
    const generated = signalsMeta.signalsLastModified || manifestState.lastModified;
    const loaded = signalsMeta.loadedAt || manifestState.loadedAt;

    const hasGenerated = Boolean(generated);
    const hasLoaded = Boolean(loaded);

    elements.freshnessGenerated.textContent = hasGenerated ? t('freshnessGenerated', { timestamp: formatDateTime(generated) }) : '';
    elements.freshnessGenerated.hidden = !hasGenerated;

    elements.freshnessLoaded.textContent = hasLoaded ? t('freshnessLoaded', { timestamp: formatDateTime(loaded) }) : '';
    elements.freshnessLoaded.hidden = !hasLoaded;

    elements.freshness.hidden = !(hasGenerated || hasLoaded);
  }

  function clearFreshness() {
    elements.freshnessGenerated.textContent = '';
    elements.freshnessLoaded.textContent = '';
    elements.freshness.hidden = true;
  }

  function persistProfile(profileId) {
    window.localStorage.setItem(LOCAL_STORAGE_KEY, profileId);
  }

  function persistLanguage(lang) {
    window.localStorage.setItem(LANG_STORAGE_KEY, lang);
  }

  function updateUrl(profileId) {
    const url = new URL(window.location.href);
    url.searchParams.set('profile', profileId);
    window.history.replaceState({}, '', url);
  }

  function updateCommand(profileId) {
    elements.commandText.textContent = `dotnet run --project src/Strategies/Gem/Gem.Cli -- --force-update --profile=${profileId}`;
  }

  function t(key, params = {}) {
    const template = translations[key] || fallbackTranslations[key] || key;
    return Object.keys(params).reduce((text, name) => text.replace(`{${name}}`, params[name]), template);
  }
})();
