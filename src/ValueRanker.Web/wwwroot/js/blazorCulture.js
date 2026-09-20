// Persists the user's chosen UI language across reloads. Read once at startup in Program.cs
// (before the culture-dependent resources are used) and written from the language switcher.

const key = "valueranker.culture";

export function getCulture() {
    try {
        return window.localStorage.getItem(key);
    } catch {
        return null;
    }
}

export function setCulture(value) {
    try {
        window.localStorage.setItem(key, value);
    } catch {
        // Ignore: if storage is blocked, the choice just won't persist across reloads.
    }
}
