(function () {
  const service = typeof ComputerUseWorkbenchJsonWsService !== 'undefined'
    ? ComputerUseWorkbenchJsonWsService
    : new ComputerUseWorkbenchJsonWsServiceService();

  const state = {
    activeRunId: '',
    pollTimer: 0
  };

  const el = {
    apiStatus: document.getElementById('apiStatus'),
    direction: document.getElementById('directionInput'),
    model: document.getElementById('modelInput'),
    maxSteps: document.getElementById('maxStepsInput'),
    screenshotScale: document.getElementById('screenshotScaleInput'),
    captureScope: document.getElementById('captureScopeInput'),
    windowTitle: document.getElementById('windowTitleInput'),
    processName: document.getElementById('processNameInput'),
    popupMode: document.getElementById('popupModeInput'),
    start: document.getElementById('startButton'),
    interrupt: document.getElementById('interruptButton'),
    refresh: document.getElementById('refreshButton'),
    runId: document.getElementById('runIdText'),
    runStatus: document.getElementById('runStatusText'),
    outputRoot: document.getElementById('outputRootText'),
    stdout: document.getElementById('stdoutLog'),
    stderr: document.getElementById('stderrLog'),
    files: document.getElementById('fileList'),
    latestScreenshot: document.getElementById('latestScreenshot'),
    screenshotEmpty: document.getElementById('screenshotEmpty'),
    runs: document.getElementById('runList')
  };

  function setStatus(text, kind) {
    el.apiStatus.textContent = text;
    el.apiStatus.className = 'status-pill ' + (kind || '');
  }

  function isActive(status) {
    return status === 'starting' || status === 'running' || status === 'interrupting';
  }

  async function loadConfig() {
    const config = normalizeKeys(await service.GetConfigAsync({ Reserved: '' }));
    el.outputRoot.textContent = config.outputRoot || '';
    el.model.value = config.defaultModel || 'gpt-5.4';
    setStatus(config.hasApiKey ? 'API key loaded' : 'Missing API key', config.hasApiKey ? 'ready' : 'error');
  }

  async function startRun() {
    const direction = el.direction.value.trim();
    if (!direction) {
      el.stdout.textContent = 'Enter a direction first.';
      return;
    }

    el.start.disabled = true;
    el.interrupt.disabled = false;
    try {
      const run = normalizeKeys(await service.StartRunAsync({
        Direction: direction,
        Model: el.model.value.trim(),
        MaxSteps: Number(el.maxSteps.value || 40),
        MaxStepsPerRun: Math.max(1, Number(el.maxSteps.value || 40)),
        ScreenshotScale: Number(el.screenshotScale.value || 1),
        CaptureScope: el.captureScope.value,
        WindowTitle: el.windowTitle.value.trim(),
        ProcessName: el.processName.value.trim(),
        PopupMode: el.popupMode.value
      }));
      state.activeRunId = run.runId;
      renderRun(run);
      startPolling();
    } catch (error) {
      el.stderr.textContent = String(error);
    } finally {
      el.start.disabled = false;
    }
  }

  async function interruptRun() {
    if (!state.activeRunId) return;
    el.interrupt.disabled = true;
    try {
      await service.InterruptRunAsync({ RunId: state.activeRunId });
      el.stderr.textContent = 'Stop requested. Waiting for the runner to exit...';
      await refreshRun();
    } catch (error) {
      el.stderr.textContent = String(error);
    }
  }

  async function refreshRun() {
    if (!state.activeRunId) {
      await loadRuns();
      return;
    }

    const run = normalizeKeys(await service.GetRunAsync({ RunId: state.activeRunId }));
    renderRun(run);
    if (!isActive(run.status)) {
      stopPolling();
    }
    await loadRuns();
  }

  async function loadRuns() {
    const runs = (await service.ListRunsAsync({ Reserved: '' })).map(normalizeKeys);
    renderRuns(runs);
  }

  function renderRun(run) {
    el.runId.textContent = run.runId || 'None';
    el.runStatus.textContent = run.status || 'Unknown';
    el.stdout.textContent = run.stdout || 'No output yet.';
    el.stderr.textContent = run.stderr || 'No errors.';
    el.interrupt.disabled = !run.runId || !isActive(run.status);
    el.start.disabled = isActive(run.status);
    renderFiles(run.files || []);
  }

  function renderFiles(files) {
    if (!files.length) {
      el.files.textContent = 'No files yet.';
      clearScreenshot();
      return;
    }

    const list = document.createElement('div');
    for (const file of files) {
      const row = document.createElement('a');
      row.href = file.url;
      row.target = '_blank';
      row.className = 'file-row';
      row.innerHTML = '<strong>' + escapeHtml(file.relativePath) + '</strong><span>' + formatBytes(file.length) + '</span>';
      list.appendChild(row);
    }
    el.files.replaceChildren(list);

    const latestScreenshot = files.find(file => /\.png$/i.test(file.name));
    if (latestScreenshot) {
      el.latestScreenshot.src = latestScreenshot.url + '?v=' + Date.now();
      el.latestScreenshot.parentElement.classList.add('has-image');
    } else {
      clearScreenshot();
    }
  }

  function renderRuns(runs) {
    if (!runs.length) {
      el.runs.textContent = 'No runs yet.';
      return;
    }

    const list = document.createElement('div');
    for (const run of runs) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'run-row';
      button.innerHTML = '<strong>' + escapeHtml(run.status) + '</strong><span>' + escapeHtml(run.runId) + '</span><small>' + escapeHtml(run.direction || '') + '</small>';
      button.addEventListener('click', () => {
        state.activeRunId = run.runId;
        renderRun(run);
        if (isActive(run.status)) startPolling();
      });
      list.appendChild(button);
    }
    el.runs.replaceChildren(list);
  }

  function clearScreenshot() {
    el.latestScreenshot.removeAttribute('src');
    el.latestScreenshot.parentElement.classList.remove('has-image');
  }

  function startPolling() {
    stopPolling();
    state.pollTimer = window.setInterval(() => refreshRun().catch(error => {
      el.stderr.textContent = String(error);
      stopPolling();
    }), 1500);
  }

  function stopPolling() {
    if (state.pollTimer) {
      window.clearInterval(state.pollTimer);
      state.pollTimer = 0;
    }
  }

  function formatBytes(bytes) {
    const value = Number(bytes || 0);
    if (value < 1024) return value + ' B';
    if (value < 1024 * 1024) return Math.round(value / 1024) + ' KB';
    return (value / 1024 / 1024).toFixed(2) + ' MB';
  }

  function escapeHtml(value) {
    return String(value || '').replace(/[&<>"']/g, ch => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#39;'
    }[ch]));
  }

  function normalizeKeys(value) {
    if (Array.isArray(value)) return value.map(normalizeKeys);
    if (!value || typeof value !== 'object') return value;
    const normalized = {};
    for (const key of Object.keys(value)) {
      const target = key.length ? key[0].toLowerCase() + key.slice(1) : key;
      normalized[target] = normalizeKeys(value[key]);
    }
    return normalized;
  }

  el.start.addEventListener('click', startRun);
  el.interrupt.addEventListener('click', interruptRun);
  el.refresh.addEventListener('click', () => refreshRun().catch(error => { el.stderr.textContent = String(error); }));

  loadConfig()
    .then(loadRuns)
    .catch(error => {
      setStatus('Config failed', 'error');
      el.stderr.textContent = String(error);
    });
})();

