// Gong di fine tempo: riproduce wwwroot/gong.mp3.
//
// I browser bloccano l'audio finché l'utente non ha interagito con la pagina.
// Qualsiasi clic, tocco o tasto lo sblocca; se al momento di suonare è ancora
// bloccato, compare in basso a destra un pulsante "Attiva audio" (lo stile sta
// in app.css, classe .audio-unlock).
(function () {
    const gongUrl = new URL('gong.mp3', document.currentScript?.src || document.baseURI).href;
    const audio = new Audio(gongUrl);
    audio.preload = 'auto';
    audio.volume = 1;

    let unlockButton = null;

    /** Prepara l'audio: da chiamare dopo una vera interazione dell'utente. */
    function unlock() {
        try {
            audio.load();
        } catch {
            /* audio non disponibile su questo dispositivo */
        }
        hideButton();
    }

    function showButton() {
        if (unlockButton || !document.body) {
            return;
        }

        unlockButton = document.createElement('button');
        unlockButton.type = 'button';
        unlockButton.className = 'audio-unlock';
        unlockButton.textContent = '🔊 Attiva audio';
        unlockButton.addEventListener('click', () => {
            unlock();
            play();
        });

        document.body.appendChild(unlockButton);
    }

    function hideButton() {
        if (unlockButton) {
            unlockButton.remove();
            unlockButton = null;
        }
    }

    /** Suona il gong. Se l'audio è bloccato mostra il pulsante e non fa rumore. */
    async function play() {
        try {
            hideButton();
            audio.pause();
            audio.currentTime = 0;
            await audio.play();
            return true;
        } catch {
            showButton();
            return false;
        }
    }

    // Qualsiasi interazione prepara l'audio.
    ['pointerdown', 'keydown', 'touchstart'].forEach(event =>
        window.addEventListener(event, unlock, { passive: true }));

    window.addEventListener('DOMContentLoaded', () => {
        try {
            audio.load();
        } catch {
            /* audio non supportato */
        }
    });

    window.gong = { play, unlock };
})();
