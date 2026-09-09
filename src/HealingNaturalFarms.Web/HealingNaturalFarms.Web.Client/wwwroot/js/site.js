// Small localStorage wrapper used by RegionState / GuestIdProvider / AuthState.
window.hnfStorage = {
    get: function (key) {
        try { return window.localStorage.getItem(key); } catch { return null; }
    },
    set: function (key, value) {
        try { window.localStorage.setItem(key, value); } catch { /* ignore */ }
    },
    remove: function (key) {
        try { window.localStorage.removeItem(key); } catch { /* ignore */ }
    }
};
