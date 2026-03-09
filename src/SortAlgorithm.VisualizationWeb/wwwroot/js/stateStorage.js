window.stateStorage = {
    /** sortvis.* キーをすべて読み込む */
    loadAll: () => {
        const prefix = 'sortvis.';
        const result = {};
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key && key.startsWith(prefix)) {
                result[key] = localStorage.getItem(key);
            }
        }
        return result;
    },

    /** 複数キーを一括書き込む */
    saveAll: (entries) => {
        for (const [key, value] of Object.entries(entries)) {
            localStorage.setItem(key, String(value));
        }
    },

    /** ブラウザの言語設定を取得する */
    getBrowserLanguage: () => {
        return navigator.language || 'en';
    }
};
