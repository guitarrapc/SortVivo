# VisualizationWeb ローディング状態 UI 仕様

## 1. 概要と背景

### 問題

Blazor WebAssembly はブラウザのメインスレッド（UI スレッド）上で動作するシングルスレッド環境である。
C# の処理がそのスレッドを占有している間、ブラウザは画面の再描画もユーザー入力の処理も行えない。
その結果、以下の操作中は **画面が固まったように見え**、ユーザーがボタンを連打したり、
意図しない操作が起きる可能性がある。

### UIがフリーズするタイミング

| タイミング | 発生箇所 | 規模感 | 対応状況 |
|-----------|---------|-------|---------|
| ソート処理の実行・記録 | `GenerateAndSortAsync()` → `SortExecutor.ExecuteAndRecordAsync()` | n=1024 のバブルソートで数秒 | ✅ 解消（非同期化 + オーバーレイ） |
| 比較アルゴリズムの追加 | `AddAlgorithmToComparison()` → `ExecuteAndRecordAsync()` | アルゴリズム数 × 単体時間 | ✅ オーバーレイ + fieldset 無効化 |
| 設定変更による全アルゴリズム再実行 | 比較モード内 `needRegeneration` フロー | アルゴリズム数 × 単体時間 | ✅ 同上（try/finally でカバー） |
| 画像ファイルのアップロード・デコード | `HandleFileUpload()` + JSInterop `setImage` | 大きな画像で数百ms | ✅ 各レンダラー内オーバーレイ |
| 画像のドラッグ＆ドロップ | `OnFileDropped()` → JSInterop `setImage` | 大きな画像で数百ms | ✅ 各レンダラー内オーバーレイ |
| 画像のレンダラー設定（WebGL↔Canvas2D 切替） | `OnRenderSettingsChanged()` + JSInterop | 数十〜数百ms | 対象外（一時的なちらつきのみ） |

### 実現可能性

**すべての要件は実現可能**。実装パターンは以下の通り。

| 要件 | Blazor での実現方法 |
|------|-------------------|
| ローディング表示 | `@if (IsLoading)` で条件レンダリング、CSS アニメーション |
| ボタンのグレーアウト | `<fieldset disabled>` で子孫フォーム要素を一括無効化 |
| コントロール全体の無効化 | `<fieldset class="controls-fieldset" disabled="@IsLoading">` でサイドバーをまとめて無効化 |
| メッセージの切り替え | `LoadingState` enum の値に応じてテキストを変える |
| UIスレッド解放 | `await Task.Yield()` の挿入（`ExecuteAndRecordAsync` に実装済み） |
| ローディング状態のトリガー | 非同期操作の前後で `_loadingState` フラグを変更し `StateHasChanged()` を呼ぶ |

---

## 2. ローディング状態の定義

`Models/LoadingState.cs` として実装済み。

```csharp
/// <summary>
/// UI のローディング状態を表す列挙型。
/// Idle 以外の状態では操作コントロールを無効化し、インジケーターを表示する。
/// </summary>
public enum LoadingState
{
    /// <summary>通常状態（操作可能）</summary>
    Idle,

    /// <summary>ソート処理の実行・記録中（Generate &amp; Sort）</summary>
    Sorting,

    /// <summary>比較モードへのアルゴリズム追加中</summary>
    AddingAlgorithm,

    /// <summary>画像ファイルのアップロード・デコード中</summary>
    LoadingImage,
}
```

---

## 3. UIフィードバックの仕様

### 3.1 ローディングオーバーレイ

#### `Index.razor`（ソート処理・比較モード）

**表示場所**: `visualization-area`（メインの可視化エリア全体）の上にオーバーレイとして重ねる。

```
┌──────────────────────────────┐
│  visualization-area           │
│                               │
│        ⠋ Sorting...           │  ← 半透明暗オーバーレイ
│                               │     スピナー + メッセージ
└──────────────────────────────┘
```

| プロパティ | 値 |
|-----------|---|
| 背景色 | `rgba(0, 0, 0, 0.6)` |
| position | `absolute; inset: 0` |
| z-index | `100`（キャンバスより前面） |
| border-radius | `8px` |
| スピナー | CSS アニメーション（`@keyframes loading-spin`、border-based） |
| スピナー色 | `#3B82F6`（青、既存のプライマリカラーに合わせる） |
| スピナーサイズ | `40px` |
| フォントサイズ | `1rem`、color: `#e5e7eb` |
| pointer-events | `all`（オーバーレイ下のクリックを遮断） |

**表示メッセージ（状態別）**:

| `LoadingState` | 表示テキスト |
|----------------|-------------|
| `Sorting` | `⏳ Sorting...` |
| `AddingAlgorithm` | `⏳ Adding algorithm...` |
| `LoadingImage` | `⏳ Loading image...` |

#### `PictureXxxRenderer`（画像ロード）

**表示場所**: 各レンダラーコンポーネントのルート `div`（`position: relative` 設定済み）の最前面。

| プロパティ | 値 |
|-----------|---|
| z-index | `30`（drag-over オーバーレイ(20)・ボタン(10)より前面） |
| border-radius | `4px`（コンテナに合わせて上書き） |
| その他 | `.loading-overlay` CSS クラスを共用 |
| クリック遮断 | `@onclick:stopPropagation="true"` |

表示テキスト: `⏳ Loading image...`

### 3.2 サイドバーコントロールの無効化

**方法**: `<fieldset class="controls-fieldset" disabled="@IsLoading">` で `sidebar-content` 内を囲む。

- HTML 標準の `fieldset[disabled]` はフォーム要素（`button`、`select`、`input`、`InputFile` 等）を
  子孫も含めてすべて無効化する
- Blazor のイベントハンドラー（`@onclick` 等）は `disabled` 状態のとき発火しない
- ユーザーには視覚的に要素が操作不能（グレーアウト）だと伝わる

**無効化の対象外**:

- サイドバー折りたたみトグルボタン（`sidebar-toggle`）
- Debug Log トグル（`fieldset` の外に配置）

**CSS リセット**:

```css
fieldset.controls-fieldset {
    border: none;
    padding: 0;
    margin: 0;
    min-inline-size: unset;
}

fieldset.controls-fieldset:disabled *,
fieldset.controls-fieldset[disabled] * {
    cursor: not-allowed;
}
```

### 3.3 ボタンの状態

`fieldset[disabled]` の適用で自動的にグレーアウトされる。
ボタンテキストの変更は `Add to Comparison` ボタンのみ行う（既存実装の拡張）。

| ボタン | 通常テキスト | ローディング中 | 変更方法 |
|--------|------------|--------------|---------|
| Generate & Sort | `🎲 Generate & Sort` | テキスト変更なし（fieldset で disabled のみ） | — |
| Add to Comparison | `➕ Add to Comparison` | `⏳ Adding...`（既存 `IsAddingAlgorithm` フラグを継続利用） | `ComparisonModeControls.razor` |

---

## 4. 実装詳細

### 4.1 `Index.razor` の変更

#### 追加フィールド

```csharp
// ローディング状態
private LoadingState _loadingState = LoadingState.Idle;
private bool IsLoading => _loadingState != LoadingState.Idle;

private string LoadingMessage => _loadingState switch
{
    LoadingState.Sorting         => "⏳ Sorting...",
    LoadingState.AddingAlgorithm => "⏳ Adding algorithm...",
    LoadingState.LoadingImage    => "⏳ Loading image...",
    _                            => "",
};
```

#### `GenerateAndSortAsync()`

```csharp
private async Task GenerateAndSortAsync()
{
    // ... metadata / pattern の null チェック（早期リターン、オーバーレイなし）

    _loadingState = LoadingState.Sorting;
    StateHasChanged();
    await Task.Yield(); // オーバーレイ表示を確定させてから重い処理を開始

    try
    {
        var (operations, statistics, actualExecutionTime) =
            await Executor.ExecuteAndRecordAsync(array, metadata);
        Playback.LoadOperations(array.AsSpan(), operations, statistics, actualExecutionTime);
    }
    catch (Exception ex)
    {
        DebugSettings.Log($"Error executing sort: {ex.Message}");
    }
    finally
    {
        _loadingState = LoadingState.Idle;
        StateHasChanged();
    }
}
```

#### `AddAlgorithmToComparison()`

```
処理フロー:
  1. IsAddingAlgorithm guard（早期リターン、オーバーレイなし）
  2. pattern null check（早期リターン、オーバーレイなし）
  3. metadata null check ← ローディング前に移動（インスタンスリスト非依存）
  4. !needRegeneration && already-added check ← 新規追加（無駄なオーバーレイを避ける）
  5. _loadingState = AddingAlgorithm → StateHasChanged() → await Task.Yield()
  6. try {
       needRegeneration ブロック（既存アルゴリズムの全再実行）
       already-added 再チェック（needRegeneration 後）← 新規追加
       await ComparisonMode.AddAlgorithmAsync()
     } finally {
       _loadingState = Idle → StateHasChanged()
     }
```

#### サイドバー HTML 構造

```razor
<div class="sidebar-content">
    <fieldset class="controls-fieldset" disabled="@IsLoading">
        <div class="statistics-panel">
            ... (全サイドバーコンテンツ)
        </div>
        ... (NormalModeControls / ComparisonModeControls)
        ... (StatisticsPanel)
    </fieldset>
</div>
```

#### ビジュアライゼーションエリアのオーバーレイ

```razor
<div class="visualization-area"> {{/* CSS に position:relative を追加済み */}}
    @if (IsLoading)
    {
        <div class="loading-overlay">
            <div class="loading-spinner"></div>
            <span>@LoadingMessage</span>
        </div>
    }
    <div class="visualization-content">
        ...
    </div>
</div>
```

### 4.2 画像ロード時のフィードバック（`PictureXxxRenderer`）

各レンダラーコンポーネント（Row / Column / Block）に独立した `_isLoadingImage` フラグを追加する。
`Index.razor` の `_loadingState` とは独立して管理する。

#### 追加フィールド

```csharp
private bool _isLoadingImage = false;
private bool _previousIsLoadingImage = false;
```

#### `ShouldRender()` への追跡追加

```csharp
protected override bool ShouldRender()
{
    // ... 既存チェック ...
    var shouldRender = _previousHasData        != hasData
                    || (hasData && !_isInitialized)
                    || _previousServiceVersion != serviceVersion
                    || _previousIsDragOver     != _isDragOver
                    || _previousIsLoadingImage != _isLoadingImage; // 追加
    // ...
    _previousIsLoadingImage = _isLoadingImage; // 追加
    return shouldRender;
}
```

#### `_isLoadingImage` の収束設計

`OnImageServiceChanged` をファイル選択・ドロップ両経路の収束点とする。

```
ファイル選択 (HandleFileUpload)
  └─ _isLoadingImage = true → StateHasChanged()
  └─ await stream.CopyToAsync() ← 重い処理（yield でオーバーレイ表示確定）
  └─ LoadImageFromDataUrl() → _imageService.AddImage()
       └─ OnChanged → InvokeAsync(OnImageServiceChanged)
                          └─ await setImage JSInterop
                          └─ _isLoadingImage = false ← ここで消える
                          └─ StateHasChanged()
  ※ 例外時のみ catch で _isLoadingImage = false にリセット

ドロップ (OnFileDropped → InvokeAsync)
  └─ _isLoadingImage = true → StateHasChanged()
  └─ LoadImageFromDataUrl() → _imageService.AddImage()
       └─ OnChanged → InvokeAsync(OnImageServiceChanged)
                          └─ await setImage JSInterop
                          └─ _isLoadingImage = false ← ここで消える
                          └─ StateHasChanged()
```

#### `HandleFileUpload()`

```csharp
private async Task HandleFileUpload(InputFileChangeEventArgs e)
{
    var file = e.File;
    if (file == null) return;

    _isLoadingImage = true;
    StateHasChanged();

    try
    {
        // ... stream 読み込み・Base64変換 ...
        LoadImageFromDataUrl(dataUrl, file.Name);
        // _isLoadingImage は OnImageServiceChanged 内でクリアされる
    }
    catch (Exception ex)
    {
        DebugSettings.Log($"Image load error: {ex.Message}");
        _isLoadingImage = false; // エラー時のみここでリセット
        StateHasChanged();
    }
}
```

#### `OnFileDropped()`

```csharp
[JSInvokable]
public void OnFileDropped(string dataUrl, string fileName, long fileSize)
{
    InvokeAsync(() =>
    {
        _isLoadingImage = true;
        StateHasChanged();
        LoadImageFromDataUrl(dataUrl, fileName);
        // _isLoadingImage は OnImageServiceChanged 内でクリアされる
        return Task.CompletedTask;
    });
}
```

#### `OnImageServiceChanged()`

```csharp
private void OnImageServiceChanged()
{
    InvokeAsync(async () =>
    {
        if (_isInitialized && State?.MainArray.Length > 0)
        {
            var current = _imageService.Current;
            if (current != null)
                await JS.InvokeVoidAsync("pictureXxxCanvasRenderer.setImage", ...);
        }
        _isLoadingImage = false; // 両経路の収束点
        StateHasChanged();
    });
}
```

#### HTML オーバーレイ

```razor
<div ...  style="position: relative; ...">
    @if (_isLoadingImage)
    {
        <div class="loading-overlay" style="z-index: 30; border-radius: 4px;"
             @onclick:stopPropagation="true">
            <div class="loading-spinner"></div>
            <span>⏳ Loading image...</span>
        </div>
    }
    ...
</div>
```

### 4.3 CSS（`wwwroot/css/app.css`）

```css
/* visualization-area: オーバーレイの基準点 */
.visualization-area {
    /* 既存スタイルに追加 */
    position: relative;
}

/* ローディングオーバーレイ */
.loading-overlay {
    position: absolute;
    inset: 0;
    z-index: 100;
    background: rgba(0, 0, 0, 0.6);
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 14px;
    color: #e5e7eb;
    font-size: 1rem;
    border-radius: 8px;
    pointer-events: all;
}

.loading-spinner {
    width: 40px;
    height: 40px;
    border: 4px solid rgba(59, 130, 246, 0.3);
    border-top-color: #3B82F6;
    border-radius: 50%;
    animation: loading-spin 0.8s linear infinite;
}

@keyframes loading-spin {
    to { transform: rotate(360deg); }
}

/* fieldset リセット */
fieldset.controls-fieldset {
    border: none;
    padding: 0;
    margin: 0;
    min-inline-size: unset;
}

fieldset.controls-fieldset:disabled *,
fieldset.controls-fieldset[disabled] * {
    cursor: not-allowed;
}
```

---

## 5. 実装スコープ（優先度付き）

### Phase 1（高優先度）: ソート処理中のフリーズ解消 + 基本インジケーター ✅ 完了

- [x] `GenerateAndSort()` を `async Task GenerateAndSortAsync()` に変更し `ExecuteAndRecordAsync()` を使用
- [x] `Models/LoadingState.cs` に `LoadingState` enum を作成
- [x] `Index.razor` に `_loadingState` / `IsLoading` / `LoadingMessage` フィールドを追加
- [x] ビジュアライゼーションエリアにローディングオーバーレイを追加
- [x] `<fieldset class="controls-fieldset" disabled="@IsLoading">` でサイドバーコントロールを一括無効化
- [x] `app.css` にスピナー・オーバーレイ・fieldset リセットのスタイルを追加
- [x] `visualization-area` に `position: relative` を追加

### Phase 2（中優先度）: 比較モードのフィードバック改善 ✅ 完了

- [x] `AddAlgorithmToComparison()` の `_loadingState` 管理を追加（metadata チェック前移動・早期リターン整理・try/finally）
- [x] `needRegeneration` フロー全体を try/finally で囲み `AddingAlgorithm` 状態でカバー
- [x] needRegeneration 後の already-added 再チェックを追加（保護ガード）

### Phase 3（低優先度）: 画像ロード中のインジケーター ✅ 完了

- [x] `PictureRowRenderer` / `PictureColumnRenderer` / `PictureBlockRenderer` に `_isLoadingImage` / `_previousIsLoadingImage` フラグを追加
- [x] 各レンダラーの `ShouldRender()` に `_isLoadingImage` の追跡を追加
- [x] `HandleFileUpload()` にローディング開始・エラー時クリアを追加
- [x] `OnFileDropped()` を `InvokeAsync` でラップしローディング開始を追加
- [x] `OnImageServiceChanged()` に `_isLoadingImage = false` の収束処理を追加
- [x] 各レンダラー HTML に z-index:30 のオーバーレイ要素を追加（`@onclick:stopPropagation` でクリック遮断）

---

## 6. 考慮事項・注意点

### Blazor WASM のレンダリングタイミング

- `_loadingState = LoadingState.Sorting; StateHasChanged();` の直後に
  `await Task.Yield();` を挿入しないと、オーバーレイが実際に表示される前に
  重い処理が始まってしまう可能性がある。
- `await Task.Yield()` により制御をブラウザに返すことで、
  `StateHasChanged` でスケジュールされた再レンダリングが実行される。
- `HandleFileUpload` では `await stream.CopyToAsync(ms)` の yield でオーバーレイが確定する。

### `SeekBar` とのインタラクション

- ローディング中はシークバーの操作も `fieldset[disabled]` で無効化される。
  ソート処理が完了していない状態でシークされると不整合が起きるため、これは望ましい。

### エラー時のクリーンアップ

- `ExecuteAndRecordAsync()` が例外をスローした場合でも `_loadingState = LoadingState.Idle` に戻す。
- `try/finally` パターンで確実にリセットする（全 Phase で実装済み）。
- 画像ロードの例外は `catch` ブロックで `_isLoadingImage = false` にリセット（`OnImageServiceChanged` が呼ばれないため）。

### `ShouldRender` と `_isLoadingImage`

- 各 PictureXxxRenderer は `ShouldRender()` をオーバーライドして不要な再レンダリングを抑制している。
- `_isLoadingImage` の変化（`true` ↔ `false`）を検知するため `_previousIsLoadingImage` で追跡する必要がある。
- これがないとオーバーレイの表示・非表示が画面に反映されない。

### `OnImageServiceChanged` の収束設計

- 画像ロード（ファイル選択・ドロップ）のどちらの経路も `_imageService.AddImage()` → `OnChanged` → `OnImageServiceChanged` に収束する。
- `OnImageServiceChanged` の末尾で `_isLoadingImage = false` を設定することで、
  JSInterop の `setImage` 完了後（最後の重い処理）までインジケーターを維持できる。
- 例外パスでは `OnImageServiceChanged` が呼ばれないため、`catch` での個別クリアが必要。

### モバイル / 低スペック環境

- `ExecuteAndRecordAsync` の `YieldIntervalMs = 16ms`（約1フレーム）は
  低スペック端末では体感上まだ重いことがある。
- ローディングオーバーレイがある前提では、ユーザーは「処理中」と認識できるため、
  現状の `YieldIntervalMs` 値のままで十分と判断する。

