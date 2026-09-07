// SPDX-License-Identifier: AGPL-3.0-only
import { createRpc } from './rpc.js';

export function attachHost(transport) {
  const rpc = createRpc(transport);
  const activations = new WeakMap();
  let engine;
  let settings;
  let osTheme = 'Light';
  const logError = error => {
    console.error('[Mesh Me360 bridge]', error);
    void rpc.request('engine.error', { message: String(error.message ?? error), fatal: false }).catch(() => {});
  };
  const applyTheme = () => {
    if (!settings || !engine) return;
    const dark = (settings.theme === 'System' ? osTheme : settings.theme) === 'Dark';
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    engine.desktopSetTheme(!dark);
  };
  const finishModel = async event => {
    const { file, success, error } = event.detail;
    const activationId = activations.get(file);
    activations.delete(file);
    if (!success) {
      await rpc.request('engine.log', { message: `File activation ${activationId ?? '(web chooser)'}: ${error ?? 'cancelled'}` });
      return;
    }
    if (activationId) await rpc.request('recent.add', { activationId });
    await rpc.request('document.titleChanged', { title: file.name });
    await rpc.request('document.stateChanged', { state: 'ReadyClean', activationId: activationId ?? null });
  };
  window.addEventListener('meshme:model-result', event => { void finishModel(event).catch(logError); });
  rpc.onMessage(message => {
    void (async () => {
      if (message.type === 'host.ready') {
        settings = message.payload.settings; osTheme = message.payload.osTheme; applyTheme();
        const autoLang = navigator.language.split('-')[0].toLowerCase();
        if (!await engine.desktopSetLanguage(settings.language === 'Auto' ? autoLang : settings.language))
          await engine.desktopSetLanguage('en');
        const selector = document.getElementById('lang-select');
        if (selector) selector.value = document.documentElement.lang;
      } else if (message.type === 'theme.osChanged') {
        osTheme = message.payload.theme; applyTheme();
      } else if (message.type === 'host.startupFile') {
        const { token, filename, contentType } = message.payload;
        if (!/^[a-f0-9]{64}$/.test(token) || typeof filename !== 'string' || /[\\/]/.test(filename)) throw new Error('Invalid native file metadata.');
        console.info(`[Mesh Me360 bridge] ${message.requestId} opening ${filename}`);
        const response = await fetch(`https://bridge.meshme360.local/file/${token}`, { cache: 'no-store', credentials: 'omit' });
        if (!response.ok) throw new Error('Native file access failed. Please reopen the file.');
        const file = new File([await response.blob()], filename, { type: contentType });
        activations.set(file, message.requestId);
        if (/\.mm360$/i.test(filename)) {
          await window.meshMe360Locked.openProject(file);
          await finishModel({ detail: {file,success:true} });
        } else if (/\.bumpmesh$/i.test(filename)) {
          const success = await engine.desktopLoadProject(file);
          await finishModel({ detail: { file, success: success === true, error: 'Project load cancelled or failed' } });
        } else await engine.desktopLoadModel(file); // Includes the preserved STEP quality dialog.
      }
    })().catch(logError);
  });
  return {
    rpc,
    async updateSettings(patch) {
      settings = await rpc.request('settings.set', patch);
      applyTheme();
      return settings;
    },
    async download(blob, filename) {
      const url = URL.createObjectURL(blob);
      try {
        const registered = await rpc.request('document.stateChanged', { state: 'Exporting', downloadUrl: url, filename });
        const anchor = document.createElement('a');
        anchor.href = url; anchor.download = filename; anchor.style.display = 'none';
        document.body.appendChild(anchor); anchor.click(); anchor.remove();
        return registered;
      } catch (error) { logError(error); }
      finally { setTimeout(() => URL.revokeObjectURL(url), 60000); }
    },
    async start(api) {
      engine = api;
      document.getElementById('theme-toggle')?.addEventListener('click', () => {
        void rpc.request('settings.set', { theme: document.documentElement.getAttribute('data-theme') === 'light' ? 'Light' : 'Dark' })
          .then(value => { settings = value; applyTheme(); }).catch(logError);
      });
      document.getElementById('lang-select')?.addEventListener('change', event => {
        void rpc.request('settings.set', { language: event.target.value }).then(value => { settings = value; }).catch(logError);
      });
      await rpc.request('web.ready', { baseline: 'a6ac179149b8a17c71a9469dd4cb6f866c0c01d1' });
    }
  };
}
