// carousel.js — SortCardGrid スマホカルーセル用 JS interop

window.carouselInterop = {
    _registrations: {},

    setup: function (id, dotNetRef) {
        const el = document.getElementById(id);
        if (!el) return;

        // 既存のハンドラーがあれば先にクリーンアップ
        this.dispose(id);

        const handler = () => {
            const cards = el.children;
            if (cards.length === 0) return;
            const cardWidth = cards[0].offsetWidth;
            const gap = 12; // sort-card-grid の gap と合わせる
            const activeIndex = Math.min(
                Math.round(el.scrollLeft / (cardWidth + gap)),
                cards.length - 1
            );
            dotNetRef.invokeMethodAsync('SetActiveCarouselIndex', activeIndex);
        };

        el.addEventListener('scroll', handler, { passive: true });
        this._registrations[id] = { el, handler, dotNetRef };
    },

    dispose: function (id) {
        const reg = this._registrations[id];
        if (reg) {
            reg.el.removeEventListener('scroll', reg.handler);
            delete this._registrations[id];
        }
    },

    scrollTo: function (id, index) {
        const el = document.getElementById(id);
        if (!el) return;
        const cards = el.children;
        if (cards.length === 0 || index < 0 || index >= cards.length) return;
        const cardWidth = cards[0].offsetWidth;
        const gap = 12;
        el.scrollTo({ left: index * (cardWidth + gap), behavior: 'smooth' });
    },

    disableScroll: function (id) {
        const reg = this._registrations[id];
        if (reg && reg.el) {
            reg.el.style.overflowX = 'hidden';
            reg.el.style.touchAction = 'none';
        }
    },

    enableScroll: function (id) {
        const reg = this._registrations[id];
        if (reg && reg.el) {
            reg.el.style.overflowX = '';
            reg.el.style.touchAction = '';
        }
    }
};
