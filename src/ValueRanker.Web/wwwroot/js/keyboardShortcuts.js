// Forwards keydown events on the page to a .NET component, ignoring keystrokes typed into
// text inputs and any combination held with a modifier key (so browser/OS shortcuts still work).

let currentHandler = null;

export function register(dotNetRef) {
    unregister();

    currentHandler = (event) => {
        const tag = event.target?.tagName;
        if (tag === "INPUT" || tag === "TEXTAREA") {
            return;
        }

        if (event.ctrlKey || event.metaKey || event.altKey) {
            return;
        }

        dotNetRef.invokeMethodAsync("OnKeyDownAsync", event.key);
    };

    window.addEventListener("keydown", currentHandler);
}

export function unregister() {
    if (currentHandler) {
        window.removeEventListener("keydown", currentHandler);
        currentHandler = null;
    }
}
