// Small helpers the Blazor app calls (browser features C# can't reach directly): saving, speech and display settings.
window.ct = {
    get(key) {
        try { return localStorage.getItem(key); } catch { return null; }
    },
    set(key, value) {
        try { localStorage.setItem(key, value); return true; } catch { return false; }
    },
    remove(key) {
        try { localStorage.removeItem(key); } catch { }
    },

    // Gives the person a file to keep (a save file).
    download(fileName, text) {
        const blob = new Blob([text], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    },

    // Read-aloud with the browser's built-in speech (Section 27).
    speechAvailable() {
        return 'speechSynthesis' in window && typeof SpeechSynthesisUtterance !== 'undefined';
    },
    speak(text) {
        if (!window.ct.speechAvailable()) return;
        window.speechSynthesis.cancel();
        window.speechSynthesis.speak(new SpeechSynthesisUtterance(text));
    },
    stopSpeaking() {
        if (window.ct.speechAvailable()) window.speechSynthesis.cancel();
    },

    // Display settings: font, text size, line spacing and reduced motion.
    applySettings(font, textScale, lineSpacing, reduceMotion, animationScale) {
        const root = document.documentElement;
        const fonts = {
            atkinson: '"Atkinson Hyperlegible", Verdana, sans-serif',
            opendyslexic: '"OpenDyslexic", "Atkinson Hyperlegible", Verdana, sans-serif',
            system: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
        };
        root.style.setProperty('--font', fonts[font] || fonts.atkinson);
        root.style.setProperty('--text-scale', String(textScale));
        root.style.setProperty('--line-spacing', String(lineSpacing));
        root.style.setProperty('--anim-scale', String(animationScale));
        root.dataset.reduceMotion = reduceMotion ? 'true' : 'false';
        if (font === 'opendyslexic' && !document.getElementById('opendyslexic-css')) {
            // OpenDyslexic isn't on Google Fonts, so it is fetched only when someone picks it.
            const link = document.createElement('link');
            link.id = 'opendyslexic-css';
            link.rel = 'stylesheet';
            link.href = 'https://fonts.cdnfonts.com/css/opendyslexic';
            document.head.appendChild(link);
        }
    },
};
