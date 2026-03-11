# SortVivo カード並び替え機能 仕様書

## 概要

SortCardGrid コンポーネントにドラッグ&ドロップによるソートカード並び替え機能を追加する。  
JavaScript で実装し、WASM との通信オーバーヘッドを最小化してスムーズな UX を提供する。

---

## 1. 目的・要件

### 1.1 主要目的
- ユーザーが複数の比較カードの表示順序を自由に変更できるようにする
- デスクトップ（グリッド表示）でのドラッグ操作を実装
- モバイル（カルーセル表示）では別の並び替え手段を検討（後述）

### 1.2 非機能要件
- **パフォーマンス**: 60fps を維持し、ドラッグ中のラグを最小化
- **レスポンシブ**: グリッドレイアウト（2列/3列）に対応
- **永続化**: 現在の URL 状態管理と同様に、並び替え順序を `cards` クエリパラメータに反映
- **アクセシビリティ**: 視覚的フィードバックを明確に

---

## 2. 動作仕様

### 2.1 ドラッグ操作（デスクトップ）

#### 2.1.1 トリガー条件
- **ドラッグハンドル**: `.sort-card__drag-handle` へのポインターダウンで即時ドラッグ開始
- **対象デバイス**: マウス、タッチスクリーン（Pointer Events API で統一）
- **対象要素**: `.sort-card__drag-handle`（ヘッダー左端の `⠿` アイコン）

#### 2.1.2 ドラッグ開始（pointerdown 時）
- **カードの状態変化**:
  - ドラッグ中のカードに `.sort-card--dragging` クラスを追加
  - `opacity: 0.3` + dashed blue outline で元の位置を視認可能に
- **ドラッグプレビュー**:
  - カードのクローンを `document.body` に追加し、ポインターに追従
  - プレビューには `.sort-card--drag-preview` クラスを付与
  - `position: fixed` + `transform: translate3d(x, y, 0)` で viewport 基準の GPU 追従
  - サイズは元カードと等倍（scale 1.0）、クリック位置オフセットを保持したまま追従（中心スナップなし）
  - 3px solid green ボーダー + `drag-pulse` アニメーション（1.5s ease-in-out infinite）を適用
  - `pointer-events: none` で他の要素との干渉を防ぐ
- **その他の準備**:
  - カルーセルのスクロールを無効化（`carouselInterop.disableScroll(gridId)`）
  - 他の全カードに `transition: transform 200ms ease-out` を適用
  - ブラウザのデフォルト動作（テキスト選択など）を `preventDefault()` で抑制

#### 2.1.3 ドラッグ中（pointermove）
- **プレビュー追従**: ポインター座標に合わせて `translate3d` を更新（クリック位置オフセットを保持）
- **並び替えシミュレート**:
  - カーソル位置が移動先カードの**元の位置の中心**を超えた時点で確定位置が切り替わる（中心対稱）
  - 全カードの transform 適用前の元の位置を基準に判定するため、ドラッグ中に他カードが移動しても座標系が変わらない
  - 他のカードを `transform: translateX/Y` でアニメーション移動（`transition: transform 200ms ease-out`）
- **スクロール境界検出**: 対象外（グリッドはビューポート内に収まる設計のため不要）

#### 2.1.4 ドロップ時（pointerup）
- **新しい順序の確定**:
  - `currentIndex`（ドラッグ中に更新された最終位置）を挿入位置とする
  - 元の位置と同じ場合は C# への通知をスキップ（No-op）
- **アニメーション・クリーンアップ**:
  - ドラッグプレビューをフェードアウト（`opacity: 0`, 100ms linear）後に DOM から削除
  - 全カードの `.sort-card--dragging` クラス、`transform`、`transition` をリセット
- **C# への通知**:
  - `dotNetRef.invokeMethodAsync('OnReorder', newOrderArray)` で新しい順序配列を送信
  - `newOrderArray` は `[0, 2, 1, 3]` のような元のインデックスの配列
- **クリーンアップ**:
  - カルーセルスクロールを再有効化（`carouselInterop.enableScroll(gridId)`）
  - `dragState` をリセット

#### 2.1.5 キャンセル条件
- **Escキー押下**: ドラッグを中止し、元の状態に復元
- **ポインターがビューポート外**: ドロップ扱いで現在位置を確定

### 2.2 モバイル対応（カルーセルモード）

#### 2.2.1 課題
- カルーセルは横スクロールが主要操作であり、ドラッグ操作と干渉する
- 長押しでのドラッグは誤操作を招きやすい（スクロール意図との区別が困難）

#### 2.2.2 実装（✅ 完了）

**案3: 編集モード + ドラッグハンドル** を採用

- コントロールバーに「並び替え」トグルボタンを配置（`SortCardGrid` の `mobile-controls`）
- 編集モード ON 時:
  - カルーセルを縦スクロールリストに切り替え（横スクロール無効化）
  - 各カードヘッダー左端の `⠿` アイコン（`.sort-card__drag-handle`）を表示
  - ハンドルへの pointerdown で即時ドラッグ開始（カルーセルスクロールとの干渉なし）
  - ハンドル以外の領域は `pointer-events: none` で誤操作防止
- 編集モード OFF 時: カルーセル横スクロールに復帰

**不採用案**:
- 案1（← → ボタン）: ドラッグ操作と UX の統一感が下がるため不採用
- 案2（QuickAccessPanel への追加）: UI 配置が不自然なため不採用

---

## 3. 永続化仕様

### 3.1 URL クエリパラメータ
- **現状**: `?cards=Quicksort|MergeSort|HeapSort` の形式で順序を保持
- **並び替え後**: カードの新しい順序を `cards` パラメータに反映
- **実装**: `SortCardGrid.OnReorder` → `Index.UpdateContentStateUrl()` の既存フローを活用

### 3.2 localStorage
- **対象外**: カード順序は保存しない（URL が単一の真実の情報源）
- 理由: ユーザーが URL を共有した際に順序も再現されるべきため

---

## 4. アニメーション仕様

### 4.1 固定パラメータ
| 項目 | 値 | 備考 |
|------|-----|------|
| カード移動アニメーション | 200ms | ease-out カーブ |
| プレビューフェードアウト | 100ms | linear |
| プレビューボーダーアニメーション | 1.5s | ease-in-out infinite（drag-pulse） |


---

## 5. アクセシビリティ

### 5.1 現時点の対応
- **視覚フィードバック**: ドラッグ中の透明度変化、プレビューの影表示、元の位置の点線枠
- **ARIA 属性**: `aria-grabbed` / `aria-dropeffect` は ARIA 1.1 で非推奨のため不採用。現状は視覚フィードバックのみで対応。

### 5.2 将来の拡張（スクリーンリーダー対応）
- `role="status"` の live region で並び替え完了をスクリーンリーダーに通知（例: "BubbleSort を位置 2 に移動しました"）

---

## 6. テスト計画

### 6.1 手動テスト項目
- [x] デスクトップ Chrome/Edge/Firefox でドラッグ操作が正常動作
- [x] モバイル Safari/Chrome で編集モード経由のドラッグが正常動作
- [x] モバイルでカルーセルスクロールとドラッグが干渉しない
- [x] 2列/3列グリッドでの並び替えが正しく動作
- [x] Esc キーでドラッグがキャンセルされる
- [x] 並び替え後 URL の `cards` パラメータが更新される
- [x] URL をコピーして新しいタブで開いた際、並び替え順序が再現される
- [x] ドラッグプレビュー要素がドロップ/キャンセル後に確実に DOM から削除される
- [x] ドラッグ中に元の位置へ戻すことができる（中心を超えない限り戻せる）

### 6.2 パフォーマンステスト
- [ ] 6枚のカードを同時にドラッグしても 60fps を維持
- [ ] ドラッグ中の CPU 使用率が 30% 以下（DevTools Performance）

---

## 7. 未決定事項・今後の検討

### 7.1 モバイル対応の方針（✅ 確定）
- 案3「編集モード + ドラッグハンドル」で実装完了（詳細は 2.2.2 参照）

### 7.2 カード数制限
- 現在の `ComparisonState.MaxComparisons = 6` を維持
- 6枚以上でのドラッグ操作は性能検証が必要

### 7.3 アニメーションのカスタマイズ
- 固定値で実装
- ユーザーからの要望があれば Settings に追加を検討

---

## 8. 実装状況

| Phase | 内容 | 状態 |
|-------|------|------|
| Phase 1 | JavaScript ドラッグロジックの実装（dragInterop.js） | ✅ 完了 |
| Phase 2 | C# との統合（SortCardGrid.razor / Index.razor） | ✅ 完了 |
| Phase 3 | CSS アニメーションの調整（drag-pulse / dragging スタイル） | ✅ 完了 |
| Phase 4 | テスト＆デバッグ（DOM蓄積バグ・座標系バグ・中心判定バグ等の修正） | ✅ 完了 |
| Phase 5 | モバイル対応（編集モード + ドラッグハンドル） | ✅ 完了 |

---

## 9. 参考リンク

- [Pointer Events API - MDN](https://developer.mozilla.org/en-US/docs/Web/API/Pointer_events)
- [ドラッグ＆ドロップ UX ベストプラクティス](https://www.nngroup.com/articles/drag-drop/)
- [CSS GPU アクセラレーション](https://www.paulirish.com/2012/why-moving-elements-with-translate-is-better-than-posabs-topleft/)

