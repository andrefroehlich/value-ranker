// Thin wrapper around window.localStorage for Blazor JS interop.
// getItem/removeItem swallow errors (e.g. storage blocked in private browsing)
// and report "no value" instead, since a missing run is a recoverable state.
// setItem lets errors (e.g. quota exceeded) propagate so the caller can report
// a clear "could not save" failure instead of silently losing data.

export function getItem(key) {
    try {
        return window.localStorage.getItem(key);
    } catch {
        return null;
    }
}

export function setItem(key, value) {
    window.localStorage.setItem(key, value);
}

export function removeItem(key) {
    try {
        window.localStorage.removeItem(key);
    } catch {
        // Ignore: if storage is blocked, there is nothing left to remove anyway.
    }
}
