// SeekBar用のJavaScript互換モジュール

window.seekBarInterop = {
  // クリック位置からパーセンテージを計算
  getClickPercentage: function (elementId, clientX) {
    const element = document.getElementById(elementId);
    if (!element) return 0;

    const rect = element.getBoundingClientRect();
    const x = clientX - rect.left;
    const percentage = Math.max(0, Math.min(1, x / rect.width));
    return percentage;
  },

  // DOM の .seek-marker 要素から現在のマーカー位置（0〜1）を取得
  _getMarkerPercentages: function (element) {
    return Array.from(element.querySelectorAll('.seek-marker'))
      .map(m => parseFloat(m.style.left))
      .filter(v => !isNaN(v))
      .map(v => v / 100);
  },

  // 指定位置の近くにマーカーがあればスナップ（threshold はバー幅に対する比率）
  _snapToMarker: function (percentage, markers, threshold) {
    let closest = null;
    let minDist = Infinity;
    for (const m of markers) {
      const dist = Math.abs(m - percentage);
      if (dist < threshold && dist < minDist) {
        minDist = dist;
        closest = m;
      }
    }
    return closest !== null ? closest : percentage;
  },

  // ドラッグ&ドロップのセットアップ
  setupDragDrop: function (elementId, dotnetHelper) {
    const element = document.getElementById(elementId);
    if (!element) return;

    let isDragging = false;

    // タップ・クリック開始時のみマーカーにスナップ（16px 以内）
    const SNAP_PX = 16;

    const updatePosition = (clientX, snap) => {
      let pct = this.getClickPercentage(elementId, clientX);
      if (snap) {
        const rect = element.getBoundingClientRect();
        const threshold = rect.width > 0 ? SNAP_PX / rect.width : 0;
        const markers = this._getMarkerPercentages(element);
        pct = this._snapToMarker(pct, markers, threshold);
      }
      dotnetHelper.invokeMethodAsync('OnDrag', pct);
    };

    // マウスイベント
    const onMouseDown = (e) => {
      isDragging = true;
      updatePosition(e.clientX, true); // クリック開始時はスナップあり
      e.preventDefault();
    };

    const onMouseMove = (e) => {
      if (isDragging) {
        updatePosition(e.clientX, false); // ドラッグ中はスナップなし
        e.preventDefault();
      }
    };

    const onMouseUp = () => {
      isDragging = false;
    };

    // タッチイベント
    const onTouchStart = (e) => {
      isDragging = true;
      updatePosition(e.touches[0].clientX, true); // タップ開始時はスナップあり
      e.preventDefault();
    };

    const onTouchMove = (e) => {
      if (isDragging) {
        updatePosition(e.touches[0].clientX, false); // ドラッグ中はスナップなし
        e.preventDefault();
      }
    };

    const onTouchEnd = () => {
      isDragging = false;
    };

    element.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);

    // passive: false でタッチ中のページスクロールを抑止する
    element.addEventListener('touchstart', onTouchStart, { passive: false });
    document.addEventListener('touchmove', onTouchMove, { passive: false });
    document.addEventListener('touchend', onTouchEnd);
    document.addEventListener('touchcancel', onTouchEnd);

    // クリーンアップ関数を返す
    return {
      dispose: () => {
        element.removeEventListener('mousedown', onMouseDown);
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
        element.removeEventListener('touchstart', onTouchStart);
        document.removeEventListener('touchmove', onTouchMove);
        document.removeEventListener('touchend', onTouchEnd);
        document.removeEventListener('touchcancel', onTouchEnd);
      }
    };
  }
};
