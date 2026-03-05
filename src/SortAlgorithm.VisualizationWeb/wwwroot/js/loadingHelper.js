window.loadingHelper = {
    fadeOutLoadingScreen: function () {
        const el = document.querySelector('.loading-screen');
        if (!el) return;
        el.classList.add('fade-out');
        el.addEventListener('animationend', () => {
            el.style.display = 'none';
        }, { once: true });
    }
};
