# VisualizationWeb Localization Specification

## Overview

SortAlgorithm.VisualizationWeb の多言語対応仕様。JSON ベースのローカライゼーションにより、ブラウザ言語の自動検出と Settings UI からの手動切り替えを実現する。

**対応言語:** 英語 (en, デフォルト)、日本語 (ja)。将来的に他言語追加可能。

**配信環境:** CloudFlare Pages (静的ファイル配信)

## Design Decisions

### JSON ベースを採用する理由

| 方式 | メリット | デメリット |
|---|---|---|
| `.resx` + `IStringLocalizer` | .NET 標準、コンパイル時チェック | 言語ごとにサテライトアセンブリ DLL が追加ダウンロード発生、WASM 初期ロード増大 |
| **JSON ベース (採用)** | 軽量、CDN キャッシュ可、遅延ロード可、言語追加はファイル追加のみ | コンパイル時キーチェックなし (テストで緩和) |
| C# 静的クラス | 強い型付け | 言語追加のたびにコード変更・再ビルド必要 |

- CloudFlare Pages は静的ファイル配信。JSON は CDN キャッシュに乗りやすく遅延ロード可能
- WASM のダウンロードサイズを増やさない
- 新言語追加は `wwwroot/locales/xx.json` を追加 + `SupportedLanguages` に 1 行追加するだけ

### アルゴリズム名は翻訳しない

"Bubble sort", "Quick sort" 等は国際的な技術用語。翻訳すると検索性・認知性が下がるため、全言語で英語表記を維持する。

### カテゴリ名は翻訳しない

"EXCHANGE", "INSERTION" 等も技術用語として英語を維持する。ただしキー定義はしておき、将来必要になれば翻訳可能。

### チュートリアル説明文は翻訳する

`AlgorithmRegistry` の `tutorialDescription` は教育的コンテンツのため翻訳対象。ローカライゼーション JSON に外出しする。

### 数値フォーマット・単位は全言語共通

`ns`, `μs`, `ms`, `s`, `ops/frame`, `FPS` は技術的単位のため翻訳しない。桁区切りは日英で同じカンマ区切りのため特別な対応は不要。

## File Structure

```
wwwroot/locales/
  en.json          ← 英語 (フォールバック / デフォルト)
  ja.json          ← 日本語
  (将来) zh.json, ko.json, ...
```

## JSON Key Design

コンポーネント/画面単位でネストしたキー構造を採用する。

### Key Naming Rules

- 第 1 レベル: コンポーネント/画面名 (`index`, `settings`, `tutorial`, ...)
- 第 2 レベル: UI 要素のセマンティックな名前
- プレースホルダー: `{variableName}` 形式

### Full Key Structure

```jsonc
{
  // ── 共通 ──
  "common": {
    "close": "Close",
    "processing": "Processing...",
    "remove": "Remove",
    "expand": "Expand",
    "collapse": "Collapse"
  },

  // ── ナビゲーション ──
  "nav": {
    "home": "Home",
    "tutorial": "Tutorial"
  },

  // ── メインページ (Index.razor) ──
  "index": {
    "title": "SortViz",
    "addGenerate": "Add & Generate",
    "regenerate": "Regenerate",
    "allCompleted": "All Completed!",
    "completedCount": "{completed}/{total} completed",
    "processingAlgo": "Processing: {name}...",
    "loadingImage": "Loading image...",
    "maxAlgorithmsReached": "Maximum {max} algorithms reached. Remove one to add a new algorithm.",
    "addToComparison": "Add this algorithm to comparison",
    "regenerateTooltip": "Regenerate array and re-run all algorithms"
  },

  // ── クイックアクセスパネル (QuickAccessPanel.razor) ──
  "quickAccess": {
    "currentAlgorithm": "CURRENT ALGORITHM",
    "sortSetup": "Sort Setup",
    "algorithm": "Algorithm",
    "learnAbout": "Learn about this algorithm",
    "arraySize": "Array Size",
    "scramblePattern": "Scramble Pattern",
    "algorithms": "Algorithms:",
    "recommended": "Recommended: {size}",
    "max": "Max: {max}",
    "appearance": "Appearance",
    "visualizationMode": "Visualization Mode",
    "modeBarChart": "Bar Chart",
    "modeCircular": "Circular",
    "modeSpiral": "Spiral",
    "modeDotPlot": "Dot Plot",
    "modeDisparityChords": "Disparity Chords",
    "modePictureRow": "Picture Row",
    "modePictureColumn": "Picture Column",
    "modePictureBlock": "Picture Block",
    "speed": "Speed",
    "opsPerFrame": "Ops Per Frame",
    "opsPerFrameUnit": "ops/frame",
    "fpsFormat": "({fps} FPS)"
  },

  // ── 設定モーダル (SettingsModal.razor) ──
  "settings": {
    "title": "Settings",
    "rendering": "Rendering",
    "renderer": "Renderer",
    "canvas2d": "Canvas 2D",
    "webgl": "WebGL",
    "webglHint": "WebGL is faster but may have compatibility issues.",
    "playback": "Playback",
    "autoReset": "Auto Reset on Complete",
    "instantMode": "Instant Mode",
    "noAnimation": "(No Animation)",
    "sound": "Sound",
    "volume": "Volume",
    "advanced": "Advanced",
    "scrollOnGenerate": "Scroll to Sort Panel after Add & Generate",
    "tabletMobile": "(Tablet / Mobile)",
    "autoRecommendedSize": "Auto Switch to Recommended Size",
    "categoryFilter": "Category Filter",
    "allCategories": "All Categories",
    "debugLog": "Debug Log",
    "language": "Language",
    "languageHint": "UI display language"
  },

  // ── ソート統計 (SortStatsSummary.razor) ──
  "stats": {
    "time": "Time",
    "comparisons": "Comparisons",
    "swaps": "Swaps",
    "progress": "Progress",
    "reads": "Reads",
    "writes": "Writes",
    "cmp": "Cmp",
    "swp": "Swp"
  },

  // ── 再生コントロール (PlayControlBar.razor) ──
  "playback": {
    "play": "Play",
    "pause": "Pause",
    "stop": "Stop",
    "soundOn": "Sound ON",
    "soundOff": "Sound OFF",
    "soundNotAvailable": "Sound not available in Comparison Mode"
  },

  // ── ソートカード (SortCard.razor) ──
  "sortCard": {
    "clickToStart": "Click \"Add & Generate\" to start visualization"
  },

  // ── チュートリアル (TutorialPage.razor) ──
  "tutorial": {
    "title": "TUTORIAL",
    "sortTarget": "Sort Target",
    "algorithm": "Algorithm",
    "speed": "Speed",
    "speedMs": "({ms} ms)",
    "computing": "Computing...",
    "selectAlgorithm": "Select an algorithm from the panel on the left.",
    "selectAlgorithmHint": "Each algorithm is demonstrated step by step using a fixed array. Some algorithms use a dedicated array suited for the explanation.",
    "tryThis": "Try this",
    "initialState": "Initial State",
    "stepOf": "Step {current} / {total}",
    "initialStateDescription": "This is the array before sorting. Follow {name} step by step.",
    "noOperations": "No recorded operations for this algorithm.",
    "fastest": "Fastest",
    "fast": "Fast",
    "normal": "Normal",
    "slow": "Slow",
    "slowest": "Slowest",
    "firstHome": "First (Home)",
    "previousLeft": "Previous (←)",
    "nextRight": "Next (→)",
    "lastEnd": "Last (End)",
    "keyboardHints": "← → Space · Home · End",
    "marble": "Marble",
    "sortingNetwork": "Sorting Network",
    "mainArray": "Main Array",
    "buckets": "Buckets",
    "recursionTree": "Recursion Tree",
    "bst": "BST",
    "avlTree": "AVL Tree",
    "heapTree": "Heap Tree",
    "weakHeap": "Weak Heap",
    "ternaryHeap": "Ternary Heap",
    "network": "Network",
    "buffer": "Buffer {id}"
  },

  // ── 404 ページ (NotFound.razor) ──
  "notFound": {
    "title": "Not Found",
    "message": "Sorry, the content you are looking for does not exist."
  },

  // ── カテゴリ名 (翻訳予約、当面英語のまま) ──
  "categories": {
    "EXCHANGE": "EXCHANGE",
    "SELECTION": "SELECTION",
    "INSERTION": "INSERTION",
    "MERGE": "MERGE",
    "HEAP": "HEAP",
    "PARTITION": "PARTITION",
    "ADAPTIVE": "ADAPTIVE",
    "DISTRIBUTION": "DISTRIBUTION",
    "NETWORK": "NETWORK",
    "TREE": "TREE",
    "JOKE": "JOKE"
  },

  // ── アルゴリズム説明文 (AlgorithmRegistry から外出し) ──
  // キーは AlgorithmRegistry の algorithmId に対応
  "algorithmDescriptions": {
    "BubbleSort": {
      "tutorial": "How it works: ..."
    }
    // ... 各アルゴリズム
  },

  // ── 配列パターン (ArrayPatternRegistry から外出し) ──
  // キーは ArrayPatternRegistry のパターン ID に対応
  "arrayPatterns": {
    "Random": {
      "name": "🎲 Random",
      "description": "Fully randomized array"
    }
    // ... 各パターン
  }
}
```

## Service Design

### LocalizationService

```csharp
// Services/LocalizationService.cs
public sealed class LocalizationService
{
    private readonly HttpClient _http;
    private JsonElement _currentStrings;
    private JsonElement _fallbackStrings; // 常に英語を保持

    public string CurrentLanguage { get; private set; } = "en";

    // サポート言語一覧。言語追加時はここに追加するだけ
    public static readonly string[] SupportedLanguages = ["en", "ja"];

    // 言語の自称表示名 (翻訳しない、各言語の自称を使う)
    public static string GetDisplayName(string lang) => lang switch
    {
        "en" => "English",
        "ja" => "日本語",
        _ => lang
    };

    // 言語変更イベント (UI 再レンダリングのトリガー)
    public event Action? OnLanguageChanged;

    // キー引き: L["settings.title"] → ネスト解決 → "Settings"
    public string this[string key] => Resolve(key);

    // プレースホルダー付き: L["index.completedCount", completed, total]
    public string this[string key, params object[] args] => FormatResolved(key, args);

    // 初期化 (ブラウザ言語検出 + JSON ロード)
    public async Task InitializeAsync(IJSRuntime js) { ... }

    // 言語切り替え (JSON ロード + localStorage 保存 + イベント発火)
    public async Task SetLanguageAsync(string lang) { ... }
}
```

**キー解決ロジック:**
1. ドット区切りで JSON のネストをたどる (`"settings.title"` → `_currentStrings["settings"]["title"]`)
2. 現在言語に該当キーがない場合 → `_fallbackStrings` (英語) から引く
3. 英語にもない場合 → キー文字列をそのまま返す (開発時のデバッグ用)

**プレースホルダー展開:**
- `{variableName}` を位置引数として順番に `string.Replace` する
- 例: `"Step {current} / {total}"` + args `[3, 10]` → `"Step 3 / 10"`

### DI 登録

```csharp
// Program.cs
builder.Services.AddSingleton<LocalizationService>();
```

### JS Interop

既存の `stateStorage.js` にブラウザ言語検出メソッドを追加:

```javascript
// wwwroot/js/stateStorage.js に追加
getBrowserLanguage: () => {
    return navigator.language || navigator.userLanguage || 'en';
}
```

`sortvis.language` キーの読み書きは既存の `loadAll` / `saveAll` でそのまま対応可能。

## Language Detection Flow

```
App 起動
  │
  ▼
LocalizationService.InitializeAsync()
  │
  ├─ localStorage に sortvis.language がある?
  │    ├─ Yes → その値を使用
  │    └─ No  → JS Interop で navigator.language を取得
  │              ├─ "ja" / "ja-JP" / "ja-*" → "ja"
  │              └─ それ以外 → "en"
  │
  ▼
該当言語の JSON を HttpClient でフェッチ
  │
  ▼
フォールバック用に en.json も常にロード (en 以外の場合)
  │
  ▼
UI レンダリング (ローカライズ済み文字列使用)
```

### ユーザーが Settings で言語変更した場合

```
SetLanguageAsync("ja") 呼び出し
  │
  ├─ 新しい言語の JSON をフェッチ
  ├─ localStorage に sortvis.language = "ja" を保存
  ├─ OnLanguageChanged イベント発火
  │
  ▼
全コンポーネント再レンダリング
```

## localStorage Key

既存の `sortvis.*` 命名規則に従う:

| キー | 値 | 例 |
|---|---|---|
| `sortvis.language` | ISO 639-1 コード | `"en"`, `"ja"` |

## Component Usage Pattern

各 Razor コンポーネントでの使い方:

```razor
@inject LocalizationService L

@* シンプルなキー引き *@
<span>@L["settings.title"]</span>

@* プレースホルダー付き *@
<span>@L["index.completedCount", completedCount, totalCount]</span>

@* title 属性 *@
<button title="@L["common.close"]">✕</button>
```

### 言語変更イベントの購読

`OnLanguageChanged` を購読して `StateHasChanged()` を呼ぶ必要がある。これは各コンポーネントで個別にやると煩雑なため、初期化タイミングで `MainLayout` がまとめてハンドルし、`CascadingValue` を使ってカスケードで更新が走るようにする。

```razor
@* MainLayout.razor *@
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        L.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        L.OnLanguageChanged -= OnLanguageChanged;
    }
}
```

## Settings UI Integration

`SettingsModal.razor` の Advanced セクションの前に Language セクションを追加:

```razor
<section class="settings-section">
    <h3>🌐 @L["settings.language"]</h3>
    <div class="settings-row">
        <label>@L["settings.languageHint"]</label>
        <div class="settings-segment">
            @foreach (var lang in LocalizationService.SupportedLanguages)
            {
                <button class="@(L.CurrentLanguage == lang ? "active" : "")"
                        @onclick="() => ChangeLanguage(lang)">
                    @LocalizationService.GetDisplayName(lang)
                </button>
            }
        </div>
    </div>
</section>
```

言語表示名は各言語の自称 (`"English"`, `"日本語"`) を使う。これは OS の言語設定やブラウザの言語選択 UI と同じ国際標準の慣例。

## Fallback Strategy

```
選択言語の JSON の該当キー
  → 見つからない場合: en.json の該当キー
  → それも見つからない場合: キー文字列をそのまま表示
```

- `en.json` は常にメモリに保持 (フォールバック用)
- 他言語は選択時にのみフェッチ
- 英語が選択されている場合はフォールバック不要 (1 つの JSON のみ)

## AlgorithmRegistry / ArrayPatternRegistry Integration

### AlgorithmRegistry

- `DisplayName` (アルゴリズム名): 翻訳しない。コード内に維持
- `Category`: 翻訳しない。コード内に維持
- `TimeComplexity`, `SpaceComplexity`: 翻訳しない。数式表現
- `TutorialDescription`: 翻訳対象。JSON の `algorithmDescriptions.{id}.tutorial` から取得

`AlgorithmRegistry` にアルゴリズム ID (文字列キー) を追加し、`LocalizationService` 経由で説明文を引く:

```csharp
// AlgorithmInfo に追加
public string AlgorithmId { get; init; } // e.g. "BubbleSort"

// 説明文取得
public string GetLocalizedTutorial(LocalizationService l)
    => l[$"algorithmDescriptions.{AlgorithmId}.tutorial"];
```

### ArrayPatternRegistry

- `Name` (パターン名): 翻訳対象。Emoji 部分はそのまま維持
- `Description`: 翻訳対象

パターンごとに ID を持ち、`arrayPatterns.{id}.name` / `arrayPatterns.{id}.description` で引く。

## Testing Strategy

### Unit Tests

- `LocalizationService` のキー解決 (ネスト、ドット区切り)
- フォールバック動作 (キーが ja.json にない場合 → en.json から返る)
- プレースホルダー展開
- 存在しないキー → キー文字列をそのまま返す

### Key Coverage Test

ビルド時またはテスト時に `en.json` と `ja.json` のキー差分を検出するテスト。翻訳漏れを防止する。

```csharp
[Fact]
public void AllKeysInEnglishExistInJapanese()
{
    var enKeys = FlattenKeys(LoadJson("en.json"));
    var jaKeys = FlattenKeys(LoadJson("ja.json"));
    var missing = enKeys.Except(jaKeys);
    Assert.Empty(missing);
}
```

### Manual Testing

- ブラウザ言語を `ja` に設定して初回アクセス → 日本語 UI
- ブラウザ言語を `en` に設定して初回アクセス → 英語 UI
- Settings で言語切り替え → 即座に UI 反映
- ページリロード → localStorage の言語設定が維持される

---

## Implementation Phases

### Phase 1: Infrastructure (新規ファイルのみ、既存コード変更なし)

**目的:** ローカライゼーション基盤の構築

**作業内容:**
1. `wwwroot/locales/en.json` を作成 (全キーを定義)
2. `wwwroot/locales/ja.json` を作成 (全キーの日本語訳)
3. `Services/LocalizationService.cs` を作成
4. `stateStorage.js` に `getBrowserLanguage` メソッド追加
5. `Program.cs` に `LocalizationService` の DI 登録追加

**検証:** `LocalizationService` がキーを正しく解決できることを確認

### Phase 2: Settings UI + Language Detection (Settings のみ変更)

**目的:** 言語切り替え機能の提供

**作業内容:**
1. `MainLayout.razor` に `LocalizationService` の初期化と `OnLanguageChanged` 購読を追加
2. `SettingsModal.razor` に Language セクション追加
3. `Index.razor` の `OnAfterRenderAsync` に `sortvis.language` の永続化を追加

**検証:** Settings で言語が切り替わり、リロード後も維持されることを確認

### Phase 3: Core Pages (メイン画面のローカライズ)

**目的:** 主要 UI ラベルのローカライズ

**対象コンポーネント:**
1. `NavMenu.razor` — "Home", "Tutorial"
2. `NotFound.razor` — 404 テキスト
3. `PlayControlBar.razor` — Play/Pause/Stop ツールチップ
4. `SortCard.razor` — 空状態テキスト
5. `SortStatsSummary.razor` — Time/Cmp/Swp ラベル
6. `Index.razor` — タイトル、ステータステキスト、ボタンラベル

**検証:** 各コンポーネントの文字列が切り替わることを確認

### Phase 4: Panels (パネル類のローカライズ)

**目的:** 設定・操作パネルのローカライズ

**対象コンポーネント:**
1. `QuickAccessPanel.razor` — Sort Setup/Appearance/Speed のラベル全般
2. `SettingsModal.razor` — セクション名、ラベル、ヒントテキスト

**検証:** パネル内の全テキストが切り替わることを確認

### Phase 5: Tutorial Page (チュートリアルのローカライズ)

**目的:** チュートリアルの UI テキストローカライズ

**対象コンポーネント:**
1. `TutorialPage.razor` — ステップ表示、速度ラベル、操作ガイド、レンダラー名

**検証:** チュートリアル全体が問題なく動作することを確認

### Phase 6: Registry Descriptions (説明文のローカライズ)

**目的:** アルゴリズム・パターンの説明文ローカライズ

**作業内容:**
1. `AlgorithmRegistry` にアルゴリズム ID を追加
2. `ArrayPatternRegistry` にパターン ID を追加
3. チュートリアル説明文を JSON から取得するように変更
4. パターン名・説明文を JSON から取得するように変更

**検証:** 説明文が言語に応じて切り替わることを確認

### Phase 7: Testing (品質保証)

**作業内容:**
1. キー網羅テスト (en.json と ja.json のキー差分検出)
2. `LocalizationService` のユニットテスト
3. ブラウザ言語検出の手動テスト
