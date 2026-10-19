/* Browser-only input boundary. No authentication or gameplay mutation. */
(function () {
  'use strict';
  let canvas = null, gameplay = false, disposed = false, pending = false, inputEpoch = 0, safeProbe = null;
  function cancelInput() { inputEpoch++; release(); }
  function safeArea() {
    if (!safeProbe && document.createElement && document.body) { safeProbe = document.createElement('div'); safeProbe.style.cssText = 'position:fixed;visibility:hidden;pointer-events:none;padding:env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom) env(safe-area-inset-left)'; document.body.appendChild(safeProbe); }
    if (!safeProbe || typeof window.getComputedStyle !== 'function') return { left: 0, right: 0, top: 0, bottom: 0 };
    const style = window.getComputedStyle(safeProbe);
    return { left: parseFloat(style.paddingLeft) || 0, right: parseFloat(style.paddingRight) || 0, top: parseFloat(style.paddingTop) || 0, bottom: parseFloat(style.paddingBottom) || 0 };
  }
  function pixelScale() { return canvas && canvas.clientWidth > 0 && canvas.width > 0 ? canvas.width / canvas.clientWidth : 1; }
  function coarsePointer() { return typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches; }
  function captured() { return !!canvas && document.pointerLockElement === canvas; }
  function release() {
    if (!captured() || typeof document.exitPointerLock !== 'function') return;
    try { const operation = document.exitPointerLock(); if (operation && operation.catch) operation.catch(function () {}); } catch (_) {}
  }
  function request() {
    if (disposed || pending || !gameplay || coarsePointer() || !canvas || !canvas.isConnected
      || canvas.ownerDocument !== document || document.hidden || !document.hasFocus() || captured()
      || typeof canvas.requestPointerLock !== 'function') return;
    pending = true;
    try {
      const operation = canvas.requestPointerLock();
      if (operation && typeof operation.then === 'function') {
        operation.then(function () { pending = false; if (!gameplay || disposed) release(); }, function () { pending = false; });
      }
    } catch (_) { pending = false; }
  }
  document.addEventListener('pointerlockchange', function () { pending = false; if (!gameplay || disposed) release(); });
  document.addEventListener('pointerlockerror', function () { pending = false; });
  document.addEventListener('visibilitychange', function () { if (document.hidden) cancelInput(); });
  window.addEventListener('blur', cancelInput);
  window.addEventListener('orientationchange', cancelInput);
  window.addEventListener('resize', cancelInput);
  window.addEventListener('pagehide', function () { disposed = true; gameplay = false; cancelInput(); });
  window.addEventListener('pageshow', function () { disposed = false; pending = false; });
  window.SPARCBrowser = Object.freeze({
    initialize(element) {
      if (canvas || !element) return;
      canvas = element;
      if (canvas.style) { canvas.style.touchAction = 'none'; canvas.style.userSelect = 'none'; canvas.style.webkitUserSelect = 'none'; }
      canvas.addEventListener('touchcancel', cancelInput);
      canvas.addEventListener('pointercancel', cancelInput);
      canvas.addEventListener('touchmove', function (event) { if (event.cancelable) event.preventDefault(); }, { passive: false });
      // Left-click belongs to the UI. Capture is explicit with Tab, so HUD clicks cannot be stolen.
      canvas.addEventListener('contextmenu', function (event) { if (gameplay || coarsePointer()) event.preventDefault(); });
      canvas.addEventListener('keydown', function (event) {
        if (event.key !== 'Tab' || !gameplay || !event.isTrusted) return;
        event.preventDefault(); if (captured()) release(); else request();
      });
    },
    setGameplay(enabled) { const was = gameplay; gameplay = !!enabled; if (!gameplay) { if (was) cancelInput(); else release(); } },
    captured, coarsePointer, safeArea, pixelScale, inputEpoch() { return inputEpoch; }
  });
}());
