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

        this._rafId = null;
        this._pendingMoveX = 0;
        this._pendingMoveY = 0;

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

        // ドラッグ開始前の状態を保存
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

        // ホットパス用キャッシュ（DOM変更前に一括読み取り）
        // pointermove ごとに querySelectorAll / getComputedStyle / DOMMatrix を呼ばないようにする
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        this.dragState.cards = cards;
        this.dragState.gridCols = this._getGridColumns();
        this.dragState.cardWidth = rect.width;
        this.dragState.cardHeight = rect.height;
        this.dragState.originalCenters = cards.map((c, i) => {
            const r = c.getBoundingClientRect();
            // ドラッグ開始時点では transform が未適用なので rect の中心がそのまま元の位置
            return { index: i, centerX: r.left + r.width / 2, centerY: r.top + r.height / 2 };
        });

        // カードに dragging クラス追加
        card.classList.add('sort-card--dragging');

        // 軽量プレビュー作成（cloneNode(true) はcanvas/select等を含み非常に重いため避ける）
        const preview = document.createElement('div');
        preview.className = 'sort-card sort-card--drag-preview';
        const algoName = card.querySelector('.sort-card__algo-select, .sort-card__algorithm-name');
        if (algoName) {
            const header = document.createElement('div');
            header.className = 'sort-card__header';
            const label = document.createElement('span');
            label.className = 'sort-card__algorithm-name';
            label.textContent = algoName.value || algoName.textContent || '';
            header.appendChild(label);
            preview.appendChild(header);
        }
        preview.style.cssText = `position:fixed;pointer-events:none;z-index:2000;width:${this.dragState.originalCardWidth}px;height:${this.dragState.originalCardHeight}px;margin:0;left:0;top:0;opacity:0.85;`;
        document.body.appendChild(preview);
        this.dragState.preview = preview;

        // 初期位置を設定（DOM追加後）
        this._updatePreviewPosition(preview, x, y);

        // カルーセルスクロール無効化（編集モード時はすでにカルーセルが無効なのでスキップ）
        if (!this.editMode && window.carouselInterop && window.carouselInterop.disableScroll) {
            window.carouselInterop.disableScroll(this.gridId);
        }

        // ドラッグ中クラスをグリッドに追加（CSSでsiblingのtransitionを一括指定）
        this.grid.classList.add('sort-card-grid--dragging');
    }

    _onPointerMove(e) {
        if (!this.dragState.isDragging) return;

        e.preventDefault();

        // 最新ポインター座標を保持し、rAF で処理（高頻度イベントを60fps上限にスロットル）
        this._pendingMoveX = e.clientX;
        this._pendingMoveY = e.clientY;

        if (this._rafId !== null) return;
        this._rafId = requestAnimationFrame(() => {
            this._rafId = null;
            if (!this.dragState.isDragging) return;

            const x = this._pendingMoveX;
            const y = this._pendingMoveY;

            // プレビュー更新
            this._updatePreviewPosition(this.dragState.preview, x, y);

            // 並び替えシミュレート
            const newIndex = this._calculateDropIndex(x, y);
            if (newIndex !== -1 && newIndex !== this.dragState.currentIndex) {
                this._reorderCards(this.dragState.draggedIndex, newIndex);
                this.dragState.currentIndex = newIndex;
            }
        });
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
        preview.style.transform = `translate3d(${x - this.dragState.initialOffsetX}px, ${y - this.dragState.initialOffsetY}px, 0)`;
    }

    _calculateDropIndex(x, y) {
        const { originalCenters, currentIndex } = this.dragState;

        // キャッシュ済みの元の位置の中心座標で最近傍カードを探す
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

            const dirX = targetCenter.centerX - currentCenter.centerX;
            const dirY = targetCenter.centerY - currentCenter.centerY;

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
        const { cards, gridCols, cardWidth, cardHeight } = this.dragState;
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
        // 保留中の rAF をキャンセル（_resetDragState より前に行う）
        if (this._rafId !== null) {
            cancelAnimationFrame(this._rafId);
            this._rafId = null;
        }

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
        this.grid.classList.remove('sort-card-grid--dragging');
        this.dragState.cards.forEach(card => {
            card.classList.remove('sort-card--dragging');
            card.style.transform = '';
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
        // 保留中の rAF をキャンセル
        if (this._rafId !== null) {
            cancelAnimationFrame(this._rafId);
            this._rafId = null;
        }

        // プレビュー削除（参照をキャプチャ）
        const preview = this.dragState.preview;
        if (preview) {
            preview.remove();
        }

        // 全カードの transform をリセット
        this.grid.classList.remove('sort-card-grid--dragging');
        const cards = this.dragState.cards ?? Array.from(this.grid.querySelectorAll('.sort-card'));
        cards.forEach(card => {
            card.classList.remove('sort-card--dragging');
            card.style.transform = '';
        });

        // カルーセル再有効化（編集モード時はスキップ）
        if (!this.editMode && window.carouselInterop && window.carouselInterop.enableScroll) {
            window.carouselInterop.enableScroll(this.gridId);
        }

        this._resetDragState();
    }

    _calculateNewOrder(fromIndex, toIndex) {
        const count = this.dragState.cards.length;
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
            draggedCard: null,
            originalCardWidth: 0,
            originalCardHeight: 0,
            initialOffsetX: 0,
            initialOffsetY: 0,
            cards: null,
            originalCenters: null,
            cardWidth: 0,
            cardHeight: 0,
            gridCols: 0
        };
    }

    dispose() {
        if (this._rafId !== null) {
            cancelAnimationFrame(this._rafId);
            this._rafId = null;
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
