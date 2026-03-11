window.loadingHelper = {
    fadeOutLoadingScreen: function () {
        const el = document.querySelector('.loading-screen');
        if (!el) return;
        el.classList.add('fade-out');
        el.addEventListener('animationend', () => {
            el.style.display = 'none';
        }, { once: true });
    },

    // Blazor WebAssemblyのローディング進捗を監視
    initializeProgress: function () {
        let totalResources = 0;
        let loadedResources = 0;

        const progressText = document.querySelector('.loading-progress-text');
        const progressFill = document.querySelector('.loading-progress-fill');

        function updateProgress() {
            const percentage = totalResources > 0 ? Math.round((loadedResources / totalResources) * 100) : 0;
            if (progressFill) {
                progressFill.style.width = `${percentage}%`;
            }
            if (progressText) {
                progressText.textContent = `Loading... ${percentage}%`;
            }
        }

        // Blazor起動をカスタマイズ
        Blazor.start({
            loadBootResource: function (type, name, defaultUri, integrity) {
                // dotnetjs等の特定リソースはデフォルトローディングを使用
                if (type === 'dotnetjs' || type === 'dotnetwasm' || type === 'configuration') {
                    totalResources++;
                    // デフォルトのfetchを使用してロード完了を監視
                    fetch(defaultUri, { cache: 'no-cache', integrity: integrity })
                        .then(() => {
                            loadedResources++;
                            updateProgress();
                        })
                        .catch(() => {
                            loadedResources++;
                            updateProgress();
                        });
                    // デフォルトローディングを使用
                    return null;
                }

                totalResources++;

                return fetch(defaultUri, { 
                    cache: 'no-cache',
                    integrity: integrity 
                }).then(response => {
                    if (!response.ok) {
                        throw new Error(`Failed to load ${name}: ${response.status}`);
                    }

                    const contentLength = response.headers.get('Content-Length');
                    if (!contentLength) {
                        // Content-Lengthがない場合は、ロード完了時にカウント
                        return response.arrayBuffer().then(buffer => {
                            loadedResources++;
                            updateProgress();
                            return new Response(buffer);
                        });
                    }

                    const total = parseInt(contentLength, 10);
                    let loaded = 0;

                    const reader = response.body.getReader();
                    const stream = new ReadableStream({
                        start(controller) {
                            function push() {
                                reader.read().then(({ done, value }) => {
                                    if (done) {
                                        loadedResources++;
                                        updateProgress();
                                        controller.close();
                                        return;
                                    }
                                    loaded += value.length;
                                    updateProgress();
                                    controller.enqueue(value);
                                    push();
                                }).catch(error => {
                                    console.error('Stream reading error:', error);
                                    controller.error(error);
                                });
                            }
                            push();
                        }
                    });

                    return new Response(stream, {
                        headers: response.headers
                    });
                }).catch(error => {
                    console.error(`Error loading ${name}:`, error);
                    // エラーでもカウントを進める
                    loadedResources++;
                    updateProgress();
                    // エラー時はデフォルトのローディングメカニズムにフォールバック
                    return null;
                });
            }
        });
    }
};

// ページ読み込み時に進捗監視を初期化
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.loadingHelper.initializeProgress();
    });
} else {
    window.loadingHelper.initializeProgress();
}
