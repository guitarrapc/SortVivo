# リンクシェアボタン仕様書

## 概要

SortVizのビジュアライゼーション画面でソートアルゴリズムの設定状態や比較状態を他者と共有するためのシェア機能を提供します。

## 目的

- 現在のページの状態（選択したアルゴリズム、配列サイズ、パターン、ビジュアライゼーションモード、比較カード）をURLで共有可能にする
- 複数のシェア方法（リンクコピー、SNS投稿）を提供する
- ワンクリック/ワンタップで共有できるシンプルなUXを実現する

## 対象プラットフォーム

### 1. リンクのコピー
- **目的**: クリップボードにURLをコピー
- **実装方法**: `navigator.clipboard.writeText(url)`
- **フォールバック**: 古いブラウザ向けに `<input>` + `document.execCommand('copy')`

### 2. X（旧Twitter）
- **目的**: Xでツイート
- **URL形式**: `https://x.com/intent/tweet?text={text}&url={url}`
- **パラメータ**:
  - `text`: シェアするテキスト（例: "SortVizでソートアルゴリズムを可視化しています！"）
  - `url`: 現在のページURL（エンコード済み）
- **動作**: 新しいウィンドウまたはタブを開く

### 3. Threads
- **目的**: Threadsに投稿
- **URL形式**: `https://threads.net/intent/post?text={encoded_text}`
- **パラメータ**:
  - `text`: シェアするテキストとURLを結合してエンコード
- **動作**: 新しいウィンドウまたはタブを開く
- **注意**: ThreadsのWeb API仕様は2024年時点で限定的なため、モバイルアプリからの共有が主流

## UI配置

### 配置場所: ヘッダー右側
現在のヘッダー構成:
```
[左空白] [SORTVIZ] [チュートリアルボタン][設定ボタン]
```

新しい構成:
```
[左空白] [SORTVIZ] [シェアボタン][チュートリアルボタン][設定ボタン]
```

### 配置理由
1. **ヘッダー右側**: グローバルアクションとして常時アクセス可能
2. **設定ボタンの左**: 設定よりも使用頻度が高いと想定
3. **チュートリアルボタンの右**: 視覚的な並び順として自然

### ボタンデザイン
- **アイコン**: シェアアイコン（SVG）
  ```svg
  <svg fill="currentColor" viewBox="0 0 24 24" height="1em" width="1em" xmlns="http://www.w3.org/2000/svg">
  <path xmlns="http://www.w3.org/2000/svg" d="M16 5.63636L14.58 6.92727L12.99 5.48182V15.6364H11.01V5.48182L9.42 6.92727L8 5.63636L12 2L16 5.63636ZM20 10.1818V20.1818C20 21.1818 19.1 22 18 22H6C4.89 22 4 21.1818 4 20.1818V10.1818C4 9.17273 4.89 8.36364 6 8.36364H9V10.1818H6V20.1818H18V10.1818H15V8.36364H18C19.1 8.36364 20 9.17273 20 10.1818Z" fill="currentColor"></path>
  </svg>
  ```
- **スタイル**: 既存のヘッダーボタン（`.header__icon-btn`）と統一
- **ホバー効果**: 既存ボタンと同様の挙動
- **アクセシビリティ**: `title` 属性で説明テキスト表示

## インタラクション仕様

### フロー
1. ユーザーがシェアボタンをクリック
2. ポップオーバー/ドロップダウンメニューが表示される
3. 3つの選択肢が表示される:
   - 📋 リンクをコピー
   - 𝕏 Xでシェア
   - 🔗 Threadsでシェア
4. ユーザーが選択肢をクリック
5. 対応するアクションを実行

### ポップオーバー仕様
- **表示位置**: ボタンの真下（右寄せ）
- **スタイル**: 
  - 背景色: 半透明の暗色（例: `rgba(0, 0, 0, 0.9)`）
  - ボーダー: ゴールドアクセント（SortVizのテーマカラー）
  - 角丸: 8px
  - ドロップシャドウ: あり
- **アニメーション**: フェードイン/アウト（200ms）
- **閉じる条件**:
  - 選択肢をクリック
  - ポップオーバー外をクリック
  - ESCキーを押下

### 選択肢のデザイン
各選択肢は以下の要素で構成:
- **アイコン**: 左側に配置
- **ラベル**: アイコンの右側
- **ホバー効果**: 背景色変更（例: `rgba(255, 255, 255, 0.1)`）

## 共有されるURL

### URLの構成
現在のページURLをそのまま使用（既存のクエリパラメータを含む）:
```
https://example.com/?algo=Quicksort&size=1024&pattern=Random&mode=DisparityChords&cards=MergeSort|HeapSort
```

### パラメータ説明
- `algo`: 選択中のアルゴリズム
- `size`: 配列サイズ
- `pattern`: 配列パターン
- `mode`: ビジュアライゼーションモード
- `cards`: 比較モードのカード一覧（`|` 区切り）

### シェアテキスト
各プラットフォーム共通のデフォルトテキスト:
```
SortVizでソートアルゴリズムを可視化！
```

多言語対応（ローカライゼーション）:
- 英語: "Visualizing sorting algorithms with SortViz!"
- 日本語: "SortVizでソートアルゴリズムを可視化！"

## 技術実装

### 使用技術
- **Blazor Component**: `ShareButton.razor`
- **JavaScript Interop**: クリップボードAPI、ウィンドウオープン
- **CSS**: ポップオーバースタイル

### JSファイル構成
新規ファイル: `wwwroot/js/shareHelper.js`
```javascript
window.shareHelper = {
    copyToClipboard: async (text) => {
        // navigator.clipboard.writeText() を使用
        // フォールバック処理を含む
    },
    openShareWindow: (url, title, width, height) => {
        // window.open() でポップアップを開く
        // 中央配置計算を含む
    }
};
```

### コンポーネント構成
- `ShareButton.razor`: メインコンポーネント
- `SharePopover.razor`: ポップオーバーUI（オプション）

### 状態管理
- ポップオーバーの開閉状態: ローカルステート（`_isOpen`）
- URL取得: `NavigationManager.Uri`
- シェアテキスト: `LocalizationService`

## アクセシビリティ

### キーボード操作
- **Tab**: ポップオーバー内の選択肢をフォーカス移動
- **Enter/Space**: フォーカス中の選択肢を実行
- **Esc**: ポップオーバーを閉じる

### スクリーンリーダー
- ボタンに `aria-label` を設定
- ポップオーバーに `role="menu"` を設定
- 各選択肢に `role="menuitem"` を設定

## エラーハンドリング

### クリップボードコピー失敗時
- トーストメッセージを表示: "リンクのコピーに失敗しました"
- コンソールにエラーログを出力

### ポップアップブロック時
- トーストメッセージを表示: "ポップアップがブロックされました。ブラウザの設定を確認してください"

## 多言語対応

### リソースキー
- `share.button`: "シェア"
- `share.copyLink`: "リンクをコピー"
- `share.copySuccess`: "リンクをコピーしました！"
- `share.copyFailed`: "リンクのコピーに失敗しました"
- `share.shareToX`: "Xでシェア"
- `share.shareToThreads`: "Threadsでシェア"
- `share.defaultText`: "SortVizでソートアルゴリズムを可視化！"

## パフォーマンス考慮事項

### レンダリング最適化
- ポップオーバーは条件付きレンダリング（`@if (_isOpen)`）
- ポップオーバー外クリックの検出はイベント委譲を使用

### JavaScript最小化
- クリップボード操作とウィンドウオープンのみにJS使用
- その他のロジックはC#で実装

## 実装優先順位

### Phase 1: 基本機能
1. シェアボタンのUI実装
2. ポップオーバーの表示/非表示
3. リンクのコピー機能

### Phase 2: SNSシェア
4. Xシェア
5. Threadsシェア

### Phase 3: 改善
6. トーストメッセージ
7. アニメーション
8. 多言語対応の完全化

## テスト項目

### 機能テスト
- [ ] シェアボタンをクリックでポップオーバーが開く
- [ ] ポップオーバー外をクリックで閉じる
- [ ] ESCキーで閉じる
- [ ] リンクのコピーが成功する
- [ ] Xシェアで正しいURLが開く
- [ ] Threadsシェアで正しいURLが開く

### ブラウザ互換性
- [ ] Chrome/Edge（最新版）
- [ ] Firefox（最新版）
- [ ] Safari（最新版）
- [ ] モバイルブラウザ（iOS Safari, Chrome Android）

### アクセシビリティ
- [ ] キーボード操作のみで全機能が使える
- [ ] スクリーンリーダーで読み上げ可能
- [ ] フォーカスインジケーターが視認できる

## 将来的な拡張案

1. **カスタムメッセージ**: ユーザーがシェアテキストを編集できる
2. **OGP対応**: SNSでリッチプレビューを表示
3. **短縮URL**: 長いクエリパラメータを短縮URLに変換
4. **その他のSNS**: Facebook、Reddit、Mastodon等の追加
5. **QRコード生成**: モバイルでの共有用

## 参考リンク

- [Web Share API](https://developer.mozilla.org/en-US/docs/Web/API/Navigator/share) - ネイティブシェア機能（将来的に検討）
- [Clipboard API](https://developer.mozilla.org/en-US/docs/Web/API/Clipboard_API)
- [X Web Intent](https://developer.x.com/en/docs/twitter-for-websites/tweet-button/guides/web-intent)
- [Threads Developer Docs](https://developers.facebook.com/docs/threads) - 公式API情報（2024年時点）
