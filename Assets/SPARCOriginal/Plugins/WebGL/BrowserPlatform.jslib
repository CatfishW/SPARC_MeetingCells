mergeInto(LibraryManager.library, {
  SPARC_BrowserGameplay: function (enabled) {
    if (window.SPARCBrowser) window.SPARCBrowser.setGameplay(enabled !== 0);
  },
  SPARC_BrowserPointer: function () {
    return window.SPARCBrowser && window.SPARCBrowser.captured() ? 1 : 0;
  },
  SPARC_BrowserTouch: function () {
    return window.SPARCBrowser && window.SPARCBrowser.coarsePointer() ? 1 : 0;
  },
  SPARC_BrowserPixelScale: function () {
    return window.SPARCBrowser && window.SPARCBrowser.pixelScale ? window.SPARCBrowser.pixelScale() : 1;
  },
  SPARC_BrowserInputEpoch: function () {
    return window.SPARCBrowser && window.SPARCBrowser.inputEpoch ? window.SPARCBrowser.inputEpoch() : 0;
  },
  SPARC_BrowserSafeInset: function (side) {
    if (!window.SPARCBrowser || !window.SPARCBrowser.safeArea) return 0;
    var area = window.SPARCBrowser.safeArea(), names = ['left','right','top','bottom'];
    var scale = window.SPARCBrowser.pixelScale ? window.SPARCBrowser.pixelScale() : 1;
    return Math.max(0, area[names[side]] || 0) * scale;
  },
  SPARC_AcceptanceSnapshot: function (pointer) {
    // Only the development-only C# probe calls this symbol. It has no command receiver.
    window.__SPARC_READONLY_QA = JSON.parse(UTF8ToString(pointer));
  }
});
