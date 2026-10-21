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
    applySettings(font, textScale, lineSpacing, reduceMotion, animationScale, theme) {
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
        root.dataset.theme = theme || 'day';
        if (font === 'opendyslexic' && !document.getElementById('opendyslexic-css')) {
            // OpenDyslexic isn't on Google Fonts, so it is fetched only when someone picks it.
            const link = document.createElement('link');
            link.id = 'opendyslexic-css';
            link.rel = 'stylesheet';
            link.href = 'https://fonts.cdnfonts.com/css/opendyslexic';
            document.head.appendChild(link);
        }
    },

    // Sound effects, made on the spot with the browser's Web Audio (no sound files).
    // Each sound is a few short notes or noise bursts. Browsers only allow sound after the first click or key press.
    sound(name, volume) {
        try {
            const AC = window.AudioContext || window.webkitAudioContext;
            if (!AC) return;
            const audio = window.ct._audio || (window.ct._audio = new AC());
            if (audio.state === 'suspended') audio.resume();
            const now = audio.currentTime;
            const master = audio.createGain();
            master.gain.value = Math.max(0, Math.min(1, volume)) * 0.5;
            master.connect(audio.destination);

            // A note: a frequency (Hz) that starts at "at" seconds, lasts "length" seconds and fades out.
            const note = (freq, at, length, type = 'sine', loud = 0.6, slideTo = null) => {
                const osc = audio.createOscillator();
                const gain = audio.createGain();
                osc.type = type;
                osc.frequency.setValueAtTime(freq, now + at);
                if (slideTo) osc.frequency.exponentialRampToValueAtTime(slideTo, now + at + length);
                gain.gain.setValueAtTime(0.0001, now + at);
                gain.gain.exponentialRampToValueAtTime(loud, now + at + 0.01);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + at + length);
                osc.connect(gain).connect(master);
                osc.start(now + at);
                osc.stop(now + at + length + 0.05);
            };
            // A noise burst (a rattle or a swish), filtered so it isn't harsh.
            const noise = (at, length, loud = 0.5, filterFreq = 2000) => {
                const buffer = audio.createBuffer(1, Math.ceil(audio.sampleRate * length), audio.sampleRate);
                const data = buffer.getChannelData(0);
                for (let i = 0; i < data.length; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / data.length);
                const source = audio.createBufferSource();
                const filter = audio.createBiquadFilter();
                const gain = audio.createGain();
                source.buffer = buffer;
                filter.type = 'bandpass';
                filter.frequency.value = filterFreq;
                gain.gain.value = loud;
                source.connect(filter).connect(gain).connect(master);
                source.start(now + at);
            };

            switch (name) {
                case 'dice': for (let i = 0; i < 6; i++) noise(i * 0.07, 0.05, 0.9, 2500 + Math.random() * 1500); break;
                case 'step': note(880, 0, 0.06, 'triangle', 0.25); break;
                case 'coin-in': note(988, 0, 0.12, 'square', 0.18); note(1319, 0.08, 0.3, 'square', 0.18); break;
                case 'coin-out': note(660, 0, 0.12, 'triangle', 0.4); note(440, 0.1, 0.25, 'triangle', 0.4); break;
                case 'card': noise(0, 0.18, 0.6, 4000); break;
                case 'jail': note(330, 0, 0.25, 'sawtooth', 0.15); note(247, 0.22, 0.25, 'sawtooth', 0.15); note(196, 0.44, 0.5, 'sawtooth', 0.15); break;
                case 'build': noise(0, 0.06, 1, 800); note(140, 0, 0.12, 'sine', 0.7); noise(0.18, 0.06, 1, 800); note(140, 0.18, 0.12, 'sine', 0.7); break;
                case 'gavel': note(180, 0, 0.15, 'sine', 0.8, 90); noise(0, 0.05, 0.9, 1200); break;
                case 'turn': note(784, 0, 0.18, 'sine', 0.35); note(1047, 0.1, 0.3, 'sine', 0.3); break;
                case 'win': [523, 659, 784, 1047].forEach((f, i) => note(f, i * 0.12, 0.35, 'triangle', 0.4)); break;
                case 'bad': note(220, 0, 0.4, 'sawtooth', 0.15, 110); break;
                case 'click': note(1200, 0, 0.04, 'triangle', 0.2); break;
            }
        } catch { /* no sound is fine */ }
    },
};
