(() => {
    const key = "dsp-net-energy:" + new URL(document.baseURI).pathname + ":";
    const assets = new Map();
    const transient = new URL(location.href).searchParams.get("transient") === "1";
    let returnFocus;
    let iconDb;

    function openIcons() {
        iconDb ??= new Promise((resolve, reject) => {
            const request = indexedDB.open(key + "icons", 1);
            request.onupgradeneeded = () => request.result.createObjectStore("settings");
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        return iconDb;
    }

    document.addEventListener("click", event => {
        if (event.target.id === "import-scenario") event.target.value = "";
    }, true);

    document.addEventListener("change", event => {
        if (event.target.id !== "game-folder") return;
        assets.clear();
        const paths = Array.from(event.target.files);
        for (const name of ["resources.assets", "sharedassets0.assets"]) {
            const file = paths.find(item => ("/" + item.webkitRelativePath).endsWith("/DSPGAME_Data/" + name));
            if (file) assets.set(name, file);
        }
        event.target.value = "";
    }, true);

    window.dsp = {
        load: () => transient ? { transient } : {
            language: localStorage.getItem(key + "language"), scenario: localStorage.getItem(key + "scenario"),
        },
        saveScenario: value => localStorage.setItem(key + "scenario", value),
        language: code => {
            document.documentElement.lang = code;
            if (!transient) localStorage.setItem(key + "language", code);
        },
        setDocumentLanguage: code => { document.documentElement.lang = code; },
        download: (name, json) => {
            const href = URL.createObjectURL(new Blob([json], { type: "application/json;charset=utf-8" }));
            const link = document.createElement("a");
            link.href = href;
            link.download = name;
            document.body.append(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(href), 30_000);
        },
        dialog: selector => {
            const dialog = document.querySelector("#inspector");
            if (!dialog.open) {
                returnFocus = document.activeElement;
                dialog.showModal();
            }
            dialog.querySelector(selector)?.focus();
        },
        closeDialog: () => {
            const dialog = document.querySelector("#inspector");
            if (dialog.open) dialog.close();
            returnFocus?.focus();
            returnFocus = undefined;
        },
        focus: selector => document.querySelector(selector)?.focus(),
        focusGenerator: () => {
            document.querySelector("#advanced").open = true;
            document.querySelector("#plants").focus();
        },
        loadIcons: async () => {
            const db = await openIcons();
            return new Promise((resolve, reject) => {
                const request = db.transaction("settings").objectStore("settings").get("images");
                request.onsuccess = () => resolve({ decided: request.result !== undefined, images: request.result ?? {} });
                request.onerror = () => reject(request.error);
            });
        },
        saveIcons: async images => {
            const db = await openIcons();
            return new Promise((resolve, reject) => {
                const transaction = db.transaction("settings", "readwrite");
                transaction.objectStore("settings").put(images, "images");
                transaction.oncomplete = resolve;
                transaction.onabort = () => reject(transaction.error);
                transaction.onerror = () => reject(transaction.error);
            });
        },
        assetSizes: () => Object.fromEntries(Array.from(assets, ([name, file]) => [name, file.size])),
        assetSlice: (name, offset, length) => {
            const url = URL.createObjectURL(assets.get(name).slice(offset, offset + length));
            try {
                // The existing asset reader uses synchronous seeks. This request only reads a local Blob URL.
                const reader = new XMLHttpRequest();
                reader.open("GET", url, false);
                reader.overrideMimeType("text/plain; charset=x-user-defined");
                reader.send();
                if (reader.status !== 200) throw new Error("The selected asset could not be read.");
                return Uint8Array.from(reader.responseText, character => character.charCodeAt(0) & 255);
            } finally {
                URL.revokeObjectURL(url);
            }
        },
        releaseAssets: () => assets.clear(),
    };

    document.addEventListener("DOMContentLoaded", () => Blazor.registerCustomEventType("dialogcancel", {
        browserEventName: "dsp-dialog-cancel",
        createEventArgs: () => ({}),
    }));
    document.addEventListener("cancel", event => {
        if (event.target.id === "inspector") {
            event.preventDefault();
            event.target.dispatchEvent(new Event("dsp-dialog-cancel", { bubbles: true }));
        }
    }, true);

    if ("serviceWorker" in navigator) {
        navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" })
            .catch(error => console.warn("Offline cache setup failed", error));
    }
})();
