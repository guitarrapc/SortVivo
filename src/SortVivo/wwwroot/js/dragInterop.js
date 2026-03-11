// dragInterop.js — SortCardGrid ドラッグ＆ドロップ並び替え用 JS interop

window.dragInterop = {
    _instances: new Map(),

    setup: function (gridId, dotNetRef) {
        if (this._instances.has(gridId)) {
            this._instances.get(gridId).dispose();
        }
        this._instances.set(gridId, new DragManager(gridId, dotNetRef));
    },

    dispose: function (gridId) {
        const instance = this._instances.get(gridId);
        if (instance) {
            instance.dispose();
            this._instances.delete(gridId);
        }
    },

    enterEditMode: function (gridId) {
        const instance = this._instances.get(gridId);
        if (instance) instance.setEditMode(true);
    },

    exitEditMode: function (gridId) {
        const instance = this._instances.get(gridId);
        if (instance) instance.setEditMode(false);
    }
};

class DragManager {
    constructor(gridId, dotNetRef) {
        this.gridId = gridId;
        this.dotNetRef = dotNetRef;
        this.grid = document.getElementById(gridId);

        if (!this.grid) {
            console.warn(`[dragInterop] Grid element not found: ${gridId}`);
            return;
        }

        this.editMode = false;

        this.dragState = {
            isDragging: false,
            draggedIndex: -1,
            currentIndex: -1,
            startX: 0,
            startY: 0,
            preview: null,
            draggedCard: null,
            originalCardWidth: 0,
            originalCardHeight: 0,
            initialOffsetX: 0,
            initialOffsetY: 0
        };

        this._boundOnPointerDown = this._onPointerDown.bind(this);
        this._boundOnPointerMove = this._onPointerMove.bind(this);
        this._boundOnPointerUp = this._onPointerUp.bind(this);
        this._boundOnKeyDown = this._onKeyDown.bind(this);

        this._bindEvents();
    }

    _bindEvents() {
        this.grid.addEventListener('pointerdown', this._boundOnPointerDown);
        document.addEventListener('pointermove', this._boundOnPointerMove);
        document.addEventListener('pointerup', this._boundOnPointerUp);
        document.addEventListener('pointercancel', this._boundOnPointerUp);
        document.addEventListener('keydown', this._boundOnKeyDown);
    }

    _onPointerDown(e) {
        // ボタンやインタラクティブ要素上でのドラッグを無効化
        const target = e.target;
        if (target.closest('button, a, input, select, textarea, [role="button"]')) {
            return;
        }

        const card = target.closest('.sort-card');
        if (!card) return;

        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        const index = cards.indexOf(card);
        if (index === -1) return;

        // カード内のクリック位置（相対オフセット）を計算
        const rect = card.getBoundingClientRect();
        const offsetX = e.clientX - rect.left;
        const offsetY = e.clientY - rect.top;

        // 長押しタイマー開始（300ms）
        this.dragState.draggedIndex = index;
        this.dragState.currentIndex = index;
        this.dragState.draggedCard = card;
        this.dragState.startX = e.clientX;
        this.dragState.startY = e.clientY;
        this.dragState.initialOffsetX = offsetX;
        this.dragState.initialOffsetY = offsetY;

        const isDragHandle = !!target.closest('.sort-card__drag-handle');

        if (isDragHandle) {
            // ドラッグハンドル: 遅延なし即座に開始（touch-action: none が CSS で設定済み）
            e.preventDefault();
            this._startDrag(card, e.clientX, e.clientY);
        }
    }

    _startDrag(card, x, y) {
        this.dragState.isDragging = true;

        // 元のカードのサイズを保存（プレビュー位置計算用）
        const rect = card.getBoundingClientRect();
        this.dragState.originalCardWidth = rect.width;
        this.dragState.originalCardHeight = rect.height;

        // カードに dragging クラス追加
        card.classList.add('sort-card--dragging');

        // プレビュー作成
        const preview = card.cloneNode(true);
        preview.classList.add('sort-card--drag-preview');
        preview.classList.remove('sort-card--dragging');
        preview.style.position = 'fixed';
        preview.style.pointerEvents = 'none';
        preview.style.zIndex = '2000';
        preview.style.width = this.dragState.originalCardWidth + 'px';
        preview.style.height = this.dragState.originalCardHeight + 'px';
        preview.style.margin = '0';
        preview.style.left = '0';
        preview.style.top = '0';
        // transform/transitionをクリア（元カードから継承された可能性があるため）
        preview.style.transform = '';
        preview.style.transition = '';
        document.body.appendChild(preview);
        this.dragState.preview = preview;

        // 初期位置を設定（DOM追加後）
        this._updatePreviewPosition(preview, x, y);

        // カルーセルスクロール無効化（編集モード時はすでにカルーセルが無効なのでスキップ）
        if (!this.editMode && window.carouselInterop && window.carouselInterop.disableScroll) {
            window.carouselInterop.disableScroll(this.gridId);
        }

        // transition を追加（他のカードのアニメーション用）
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        cards.forEach(c => {
            if (c !== card) {
                c.style.transition = 'transform 200ms ease-out';
            }
        });

        // ブラウザにリフローを強制（transition が確実に適用されるように）
        if (cards.length > 0) {
            void cards[0].offsetHeight;
        }
    }

    _onPointerMove(e) {
        if (!this.dragState.isDragging) return;

        e.preventDefault();

        // プレビュー更新
        this._updatePreviewPosition(this.dragState.preview, e.clientX, e.clientY);

        // 並び替えシミュレート
        const newIndex = this._calculateDropIndex(e.clientX, e.clientY);
        if (newIndex !== -1 && newIndex !== this.dragState.currentIndex) {
            this._reorderCards(this.dragState.draggedIndex, newIndex);
            this.dragState.currentIndex = newIndex;
        }
    }

    _onPointerUp(e) {
        if (!this.dragState.isDragging) {
            this._resetDragState();
            return;
        }

        e.preventDefault();
        this._finalizeDrop();
    }

    _onKeyDown(e) {
        if (e.key === 'Escape' && this.dragState.isDragging) {
            this._cancelDrag();
        }
    }

    _updatePreviewPosition(preview, x, y) {
        if (!preview) return;

        // クリックした位置のオフセットを維持する（等倍表示）
        const scale = 1.0;
        const scaledOffsetX = this.dragState.initialOffsetX * scale;
        const scaledOffsetY = this.dragState.initialOffsetY * scale;

        preview.style.transform = `translate3d(${x - scaledOffsetX}px, ${y - scaledOffsetY}px, 0) scale(${scale})`;
    }

    _calculateDropIndex(x, y) {
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        const currentIndex = this.dragState.currentIndex;

        // 全カードの元の位置（transform除去後）の中心座標で統一して計算
        // これにより、currentIndexが変わっても座標系が一貫する
        const originalCenters = cards.map((card, i) => {
            const rect = card.getBoundingClientRect();
            const style = window.getComputedStyle(card);
            const matrix = new DOMMatrixReadOnly(style.transform);
            // transform除去後の元の位置の中心（draggedIndex含む全カード統一）
            const centerX = rect.left + rect.width / 2 - matrix.m41;
            const centerY = rect.top + rect.height / 2 - matrix.m42;
            return { index: i, centerX, centerY };
        });

        // カーソル位置から最も近い「元の位置」のカードを見つける
        let closestIndex = currentIndex;
        let minDistance = Infinity;

        for (const center of originalCenters) {
            const distance = Math.hypot(x - center.centerX, y - center.centerY);
            if (distance < minDistance) {
                minDistance = distance;
                closestIndex = center.index;
            }
        }

        // 現在のインデックスと異なる場合、そのカードの元の位置の中心を超えたかチェック
        if (closestIndex !== currentIndex) {
            const targetCenter = originalCenters[closestIndex];
            const currentCenter = originalCenters[currentIndex];

            // 現在の元の位置から対象の元の位置への方向ベクトル
            const dirX = targetCenter.centerX - currentCenter.centerX;
            const dirY = targetCenter.centerY - currentCenter.centerY;

            // カーソルの位置ベクトル（現在の元の位置からの相対）
            const toCursorX = x - currentCenter.centerX;
            const toCursorY = y - currentCenter.centerY;

            // 内積による投影で、カーソルが対象の中心を超えたかチェック
            const dotProduct = dirX * toCursorX + dirY * toCursorY;
            const targetDistanceSq = dirX * dirX + dirY * dirY;
            const progress = dotProduct / targetDistanceSq;

            if (progress >= 1.0) {
                return closestIndex;
            }
        }

        return currentIndex;
    }

    _reorderCards(draggedIndex, newIndex) {
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        const gridCols = this._getGridColumns();
        const cardWidth = cards[0]?.offsetWidth || 0;
        const cardHeight = cards[0]?.offsetHeight || 0;
        const gap = 12; // grid gap と一致させる

        cards.forEach((card, i) => {
            if (i === draggedIndex) return; // ドラッグ中カードはスキップ

            // 常に元の配置からの変換を計算（累積変換を避ける）
            let targetIndex = i;

            // draggedIndex のカードが newIndex に移動すると仮定した場合の、
            // 各カードの最終的な位置を計算
            if (draggedIndex < newIndex) {
                // ドラッグ中のカードが右/下へ移動
                // draggedIndex+1 〜 newIndex の範囲にあるカードは左/上へシフト
                if (i > draggedIndex && i <= newIndex) {
                    targetIndex = i - 1;
                }
            } else if (draggedIndex > newIndex) {
                // ドラッグ中のカードが左/上へ移動
                // newIndex 〜 draggedIndex-1 の範囲にあるカードは右/下へシフト
                if (i >= newIndex && i < draggedIndex) {
                    targetIndex = i + 1;
                }
            }

            // 元の位置とターゲット位置の差分を計算
            const currentRow = Math.floor(i / gridCols);
            const currentCol = i % gridCols;
            const targetRow = Math.floor(targetIndex / gridCols);
            const targetCol = targetIndex % gridCols;

            const translateX = (targetCol - currentCol) * (cardWidth + gap);
            const translateY = (targetRow - currentRow) * (cardHeight + gap);

            if (translateX !== 0 || translateY !== 0) {
                card.style.transform = `translate3d(${translateX}px, ${translateY}px, 0)`;
            } else {
                card.style.transform = '';
            }
        });
    }

    _finalizeDrop() {
        const fromIndex = this.dragState.draggedIndex;
        const toIndex = this.dragState.currentIndex;

        // プレビューフェードアウト（参照をキャプチャして確実に削除）
        const preview = this.dragState.preview;
        if (preview) {
            preview.style.transition = 'opacity 100ms linear';
            preview.style.opacity = '0';
            setTimeout(() => {
                preview.remove();
            }, 100);
        }

        // dragging クラス削除と transform リセット
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        cards.forEach(card => {
            card.classList.remove('sort-card--dragging');
            card.style.transform = '';
            card.style.transition = '';
        });

        // C# へ新しい順序を通知（変更があった場合のみ）
        if (fromIndex !== toIndex) {
            const newOrder = this._calculateNewOrder(fromIndex, toIndex);
            this.dotNetRef.invokeMethodAsync('OnReorder', newOrder);
        }

        // カルーセル再有効化（編集モード時はスキップ）
        if (!this.editMode && window.carouselInterop && window.carouselInterop.enableScroll) {
            window.carouselInterop.enableScroll(this.gridId);
        }

        this._resetDragState();
    }

    _cancelDrag() {
        // プレビュー削除（参照をキャプチャ）
        const preview = this.dragState.preview;
        if (preview) {
            preview.remove();
        }

        // 全カードの transform をリセット
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        cards.forEach(card => {
            card.classList.remove('sort-card--dragging');
            card.style.transform = '';
            card.style.transition = '';
        });

        // カルーセル再有効化（編集モード時はスキップ）
        if (!this.editMode && window.carouselInterop && window.carouselInterop.enableScroll) {
            window.carouselInterop.enableScroll(this.gridId);
        }

        this._resetDragState();
    }

    _calculateNewOrder(fromIndex, toIndex) {
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        const count = cards.length;
        const order = Array.from({ length: count }, (_, i) => i);
        const [moved] = order.splice(fromIndex, 1);
        order.splice(toIndex, 0, moved);
        return order;
    }

    setEditMode(enabled) {
        this.editMode = enabled;
    }

    _getGridColumns() {
        const style = window.getComputedStyle(this.grid);
        const cols = style.gridTemplateColumns.split(' ').filter(v => v !== 'auto').length;
        return cols || 1;
    }

    _resetDragState() {
        this.dragState = {
            isDragging: false,
            draggedIndex: -1,
            currentIndex: -1,
            startX: 0,
            startY: 0,
            preview: null,
            longPressTimer: null,
            draggedCard: null,
            originalCardWidth: 0,
            originalCardHeight: 0,
            initialOffsetX: 0,
            initialOffsetY: 0
        };
    }

    dispose() {
        if (this.dragState.longPressTimer) {
            clearTimeout(this.dragState.longPressTimer);
        }
        this._cancelDrag();

        if (this.grid) {
            this.grid.removeEventListener('pointerdown', this._boundOnPointerDown);
        }
        document.removeEventListener('pointermove', this._boundOnPointerMove);
        document.removeEventListener('pointerup', this._boundOnPointerUp);
        document.removeEventListener('pointercancel', this._boundOnPointerUp);
        document.removeEventListener('keydown', this._boundOnKeyDown);
    }
}
