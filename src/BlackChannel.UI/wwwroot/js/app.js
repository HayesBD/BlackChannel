// Small UI helpers (no crypto here — see crypto.js).
window.blackchannelApp = {
    // HEAD-checks a same-origin file; true if it exists (200). Used by the Download page
    // so native-package buttons only show when the package is actually present.
    async fileExists(url) {
        try {
            const r = await fetch(url, { method: 'HEAD' });
            return r.ok;
        } catch {
            return false;
        }
    }
};
