/* Newly authored PING appearance adapter. Credentials never enter the Unity runtime. */
(function () {
  'use strict';
  const ORIGIN = 'https://ping.agaii.org';
  const MODULE = 'sparc-meetingcells';
  const API = '/api/avatars/v1/me';
  const LIMIT = 32768;
  let unity = null, epoch = 0, controller = null, identity = '', revision = -1;
  let refreshTimer = null, disposed = false;
  const supported = location.origin === ORIGIN;
  const appearanceChannel = supported && typeof BroadcastChannel === 'function'
    ? new BroadcastChannel('sparc-appearance-v1') : null;

  function portalCredential() {
    if (!supported) return '';
    try {
      const value = window.localStorage.getItem('access_token');
      return typeof value === 'string' && value.length >= 16 && value.length <= 8192 && !/[\r\n]/.test(value) ? value : '';
    } catch (_) { return ''; }
  }

  function deliver(profile) {
    if (!unity || disposed) return;
    const payload = JSON.stringify(profile);
    if (payload.length <= LIMIT) unity.SendMessage('SPARC Original Appearance', 'ReceiveAppearance', payload);
  }

  function cancel() {
    epoch++;
    if (controller) controller.abort();
    controller = null;
  }

  function reset() {
    cancel(); identity = ''; revision = -1;
    deliver({ status: 'signed_out' });
  }

  function signal(type) {
    if (!supported || window.parent === window || disposed) return;
    window.parent.postMessage({
      channel: 'ping.game.bridge', source: 'ping-game', type,
      messageId: crypto.randomUUID(),
      payload: { moduleId: MODULE, version: '0.2.0', appearanceSchema: 'layerlab-casual-v1' }
    }, ORIGIN);
  }

  async function boundedJson(response) {
    const size = response.headers.get('content-length');
    if (size && (!Number.isFinite(Number(size)) || Number(size) > LIMIT)) throw new Error('Profile limit');
    if (!(response.headers.get('content-type') || '').toLowerCase().includes('application/json')) throw new Error('Profile format');
    if (!response.body || typeof response.body.getReader !== 'function') throw new Error('Profile stream');
    const reader = response.body.getReader();
    const chunks = []; let total = 0;
    try {
      for (;;) {
        const part = await reader.read();
        if (part.done) break;
        total += part.value.byteLength;
        if (total > LIMIT) { await reader.cancel(); throw new Error('Profile limit'); }
        chunks.push(part.value);
      }
    } finally { reader.releaseLock(); }
    const bytes = new Uint8Array(total); let offset = 0;
    for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
    return JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes));
  }

  function cleanProfile(value) {
    if (!value || !Number.isSafeInteger(value.revision) || value.revision < 0) return null;
    const appearance = value.appearance;
    if (!appearance || appearance.schemaVersion !== 1 || appearance.catalogVersion !== 'layerlab-casual-v1'
      || typeof appearance.presetId !== 'string' || appearance.presetId.length > 100
      || !Number.isInteger(appearance.skinIndex) || appearance.skinIndex < 0 || appearance.skinIndex > 4
      || !Array.isArray(appearance.parts) || appearance.parts.length < 5 || appearance.parts.length > 40
      || appearance.parts.some(id => typeof id !== 'string' || id.length < 1 || id.length > 128 || /[\u0000-\u001f]/.test(id))
      || new Set(appearance.parts).size !== appearance.parts.length) return null;
    // Explicit projection prevents unexpected server/account fields from crossing into Unity.
    return { status: 'ready', revision: value.revision, appearance: {
      schemaVersion: 1, catalogVersion: appearance.catalogVersion, presetId: appearance.presetId,
      skinIndex: appearance.skinIndex, parts: appearance.parts.slice()
    } };
  }

  async function requestAppearance() {
    if (!unity || disposed) return;
    const token = portalCredential();
    if (!token) { reset(); signal('auth_request'); return; }
    if (token !== identity) {
      reset(); identity = token;
    } else cancel();
    const own = epoch;
    const active = new AbortController(); controller = active;
    const timeout = setTimeout(() => active.abort(), 10000);
    try {
      const response = await fetch(API, {
        method: 'GET', headers: { Authorization: 'Bearer ' + token, Accept: 'application/json' },
        cache: 'no-store', credentials: 'omit', redirect: 'error', signal: active.signal
      });
      if (disposed || own !== epoch || portalCredential() !== token) return;
      if (response.status === 401 || response.status === 403) { reset(); return; }
      if (!response.ok || response.redirected) throw new Error('Profile unavailable');
      const profile = cleanProfile(await boundedJson(response));
      if (disposed || own !== epoch || portalCredential() !== token) return;
      if (!profile) throw new Error('Profile invalid');
      if (profile.revision < revision) return;
      revision = profile.revision;
      deliver(profile);
    } catch (_) {
      if (!disposed && own === epoch && portalCredential() === token) deliver({ status: 'unavailable' });
    } finally {
      clearTimeout(timeout);
      if (controller === active) controller = null;
    }
  }

  function refreshSoon() {
    if (refreshTimer != null) clearTimeout(refreshTimer);
    refreshTimer = setTimeout(() => { refreshTimer = null; if (!document.hidden) requestAppearance(); }, 180);
  }

  window.addEventListener('message', event => {
    if (!supported || window.parent === window || event.source !== window.parent || event.origin !== ORIGIN) return;
    const data = event.data;
    if (!data || data.channel !== 'ping.game.bridge' || data.source !== 'ping-react' || data.type !== 'auth_context') return;
    const context = data.payload;
    if (!context || context.schema !== 'ping.auth_context.v1' || context.moduleId !== MODULE) return;
    if (context.user && context.user.isAuthenticated === false) reset();
    else refreshSoon();
    // No token, URL, appearance, or account identifier from a message is trusted as an API argument.
  });
  window.addEventListener('storage', event => {
    if (event.key !== 'access_token' && event.key !== null) return;
    if (!portalCredential()) reset(); else refreshSoon();
  });
  window.addEventListener('focus', refreshSoon);
  document.addEventListener('visibilitychange', () => { if (!document.hidden) refreshSoon(); });
  if (appearanceChannel) appearanceChannel.onmessage = event => {
    if (event.data && event.data.type === 'appearance-changed') refreshSoon();
  };
  function detach() {
    disposed = true; cancel(); identity = ''; unity = null;
    if (refreshTimer != null) clearTimeout(refreshTimer);
    if (appearanceChannel) appearanceChannel.close();
  }
  window.addEventListener('pagehide', detach);
  window.SPARCMeetingCells = Object.freeze({
    attach(instance) {
      if (disposed || !instance || typeof instance.SendMessage !== 'function') return;
      unity = instance; signal('game_ready'); return requestAppearance();
    },
    requestAppearance, detach
  });
}());
