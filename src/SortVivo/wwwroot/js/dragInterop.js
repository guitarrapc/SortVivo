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

        this.dragState.longPressTimer = setTimeout(() => {
            this._startDrag(card, e.clientX, e.clientY);
        }, 300);

        // スクロールやテキスト選択を防止
        e.preventDefault();
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
        document.body.appendChild(preview);
        this.dragState.preview = preview;

        // 初期位置を設定（DOM追加後）
        this._updatePreviewPosition(preview, x, y);

        // カルーセルスクロール無効化
        if (window.carouselInterop && window.carouselInterop.disableScroll) {
            window.carouselInterop.disableScroll(this.gridId);
        }

        // transition を追加（他のカードのアニメーション用）
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        cards.forEach(c => {
            if (c !== card) {
                c.style.transition = 'transform 200ms ease-out';
            }
        });
    }

    _onPointerMove(e) {
        // 長押し検出前の移動判定（5px 以上動いたらキャンセル）
        if (this.dragState.longPressTimer && !this.dragState.isDragging) {
            const dx = e.clientX - this.dragState.startX;
            const dy = e.clientY - this.dragState.startY;
            if (Math.abs(dx) > 5 || Math.abs(dy) > 5) {
                clearTimeout(this.dragState.longPressTimer);
                this.dragState.longPressTimer = null;
            }
            return;
        }

        if (!this.dragState.isDragging) return;

        e.preventDefault();

        // プレビュー更新
        this._updatePreviewPosition(this.dragState.preview, e.clientX, e.clientY);

        // 並び替えシミュレート
        const newIndex = this._calculateDropIndex(e.clientX, e.clientY);
        if (newIndex !== -1 && newIndex !== this.dragState.currentIndex) {
            this._reorderCards(this.dragState.draggedIndex, this.dragState.currentIndex, newIndex);
            this.dragState.currentIndex = newIndex;
        }
    }

    _onPointerUp(e) {
        if (this.dragState.longPressTimer) {
            clearTimeout(this.dragState.longPressTimer);
            this.dragState.longPressTimer = null;
        }

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
        const rects = cards.map(c => c.getBoundingClientRect());

        // プレビュー中心がどのカードの上にあるかチェック
        for (let i = 0; i < rects.length; i++) {
            const rect = rects[i];
            if (x >= rect.left && x <= rect.right &&
                y >= rect.top && y <= rect.bottom) {
                return i;
            }
        }

        return this.dragState.currentIndex; // 該当なしは現在位置を維持
    }

    _reorderCards(draggedIndex, oldIndex, newIndex) {
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        const gridCols = this._getGridColumns();
        const cardWidth = cards[0]?.offsetWidth || 0;
        const cardHeight = cards[0]?.offsetHeight || 0;
        const gap = 12; // grid gap と一致させる

        cards.forEach((card, i) => {
            if (i === draggedIndex) return; // ドラッグ中カードはスキップ

            let targetIndex = i;

            // 並び替えロジック
            if (oldIndex < newIndex) {
                // 右/下へ移動
                if (i > oldIndex && i <= newIndex) {
                    targetIndex = i - 1;
                }
            } else if (oldIndex > newIndex) {
                // 左/上へ移動
                if (i >= newIndex && i < oldIndex) {
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

        // プレビューフェードアウト
        if (this.dragState.preview) {
            this.dragState.preview.style.transition = 'opacity 100ms linear';
            this.dragState.preview.style.opacity = '0';
            setTimeout(() => {
                this.dragState.preview?.remove();
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

        // カルーセル再有効化
        if (window.carouselInterop && window.carouselInterop.enableScroll) {
            window.carouselInterop.enableScroll(this.gridId);
        }

        this._resetDragState();
    }

    _cancelDrag() {
        // プレビュー削除
        if (this.dragState.preview) {
            this.dragState.preview.remove();
        }

        // 全カードの transform をリセット
        const cards = Array.from(this.grid.querySelectorAll('.sort-card'));
        cards.forEach(card => {
            card.classList.remove('sort-card--dragging');
            card.style.transform = '';
            card.style.transition = '';
        });

        // カルーセル再有効化
        if (window.carouselInterop && window.carouselInterop.enableScroll) {
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
