# URL 入力のセキュリティ・堅牢性分析

**対象ファイル:** `src/SortAlgorithm.VisualizationWeb/Pages/Index.razor`
**調査日:** 2026-03-10

---

## 概要

`Index.razor` の `OnInitialized` で URL クエリ文字列を手動パースしている（`uri.Query.TrimStart('?')` → `Split('&')` → `Split('=', 2)`）。Blazor の `[SupplyParameterFromQuery]` を使用せず自前実装のため、バリデーション漏れのリスクがある。

対象コード箇所: `OnInitialized` 内のクエリパース処理。

---

## 修正済みの問題

### ✅ 1. `size` — 負数・ゼロでクラッシュ

**入力例:** `/?size=-1` / `/?size=0`

**問題:** `int.TryParse` 成功時に値検証なしで `_arraySize = size` に代入。その後 `pattern.Generator(size, new Random())` に負値が渡り `ArgumentOutOfRangeException` でクラッシュ。

**修正:** `size >= 1 && size <= 8192` で範囲チェック済み。

---

### ✅ 2. `size` — 極端に大きい値による DoS

**入力例:** `/?size=2147483647`

**問題:** `_arraySize` 自体に上限なく、`MaxElements` の設定次第では巨大配列が生成されメモリ・CPU を大量消費。

**修正:** `size <= 8192` の上限チェック済み。

---

### ✅ 3. `cards` — `|` 区切り大量エントリによる無駄な処理

**入力例:** `/?cards=A|B|C|...(数千件)`

**問題:** `Split('|')` の全エントリに対して `GetAllAlgorithms()` の LINQ 全走査（O(n×m)）が実行される。

**修正:** `.Take(ComparisonState.MaxComparisons)` で上限カット済み。

---

### ✅ 4. 同一キーの重複（後勝ち上書き）

**入力例:** `/?size=10&size=9999999`

**問題:** `switch` で後の値が前の値を上書きする暗黙の動作。

**修正:** `seenKeys`（`HashSet<string>`）を導入し、`algo`/`size`/`pattern`/`mode` は最初の値のみ採用済み。

---

### ℹ️ 5. `+` と `%2B` のデコード混在（据え置き）

**問題:** `var val = Uri.UnescapeDataString(parts[1].Replace("+", " "))` の順番が概ね正しいが、将来リファクタ時に逆順にすると `%2B` がスペースとして解釈される罠になる。

**現状:** `pattern`/`algo`/`cards` はすべてホワイトリスト照合されるため実害なし。据え置き。

---

### ✅ 6. `mode` — 未定義整数値の受け入れ

**入力例:** `/?mode=9999` / `/?mode=-1`

**問題:** `Enum.TryParse<VisualizationMode>` は名前にない整数値でも `true` を返す。定義外の値が代入されると `SortCard.razor` の `if/else if` 分岐でフォールバックなしになりビジュアライズ領域が空白になる。さらに `UpdateContentStateUrl()` が無効な enum 値を URL に書き戻すため URL が永続的に汚染される。

**修正:** `Enum.IsDefined(mode)` チェック追加済み。

---

### ✅ 7. 複数 `&cards=` パラメータの累積に上限なし

**入力例:** `/?cards=Quicksort|Mergesort&cards=Heapsort|Timsort&cards=...(1000回繰り返し)`

**問題:** `seenKeys` ガードが `cards` には適用されない設計のため、`cards=` パラメータの個数自体に上限がなく最大6000回の LINQ 全走査が発生する。

**修正:** `parsedCards.Count < ComparisonState.MaxComparisons` による累積上限チェック追加済み。

---

### ✅ 8. `OnInitialized` — 不正 `%XX` で `UriFormatException` → ページクラッシュ

**入力例:** `/?algo=%GG` / `/?size=%` / `/?cards=Quicksort|%ZZ`

**問題:** `Uri.UnescapeDataString` は不正なパーセントエンコードに対して `UriFormatException` をスローする。`OnInitialized` に try-catch がないため例外が Blazor エラーハンドラに伝播し、ページが「An unhandled error has occurred.」表示になる。

**修正:** `OnInitialized` のクエリパース全体を `try-catch (UriFormatException)` で囲み、デフォルト値で続行するよう修正済み。

---

### ✅ 9. `TutorialPage.razor` — ルートパラメータ `AlgorithmName` の `UriFormatException`

**入力例:** `/tutorial/%GG`

**問題:** `OnParametersSetAsync` 内の `Uri.UnescapeDataString(AlgorithmName)` が不正エンコードで例外スロー。XSS・ヌル文字はホワイトリスト照合により無害化される。`Index.razor` からの正規遷移は `Uri.EscapeDataString` 済みのため問題ない。URL 直打ちのみが対象。

**修正:** `OnParametersSetAsync` の本体を `try-catch (UriFormatException)` で囲み、アルゴリズム未選択のデフォルト表示にフォールバックするよう修正済み。

---

## 残存問題

### ✅ 1. `UriFormatException` catch 後の部分的な状態適用（深刻度: 低）

**入力例:**
```
/?cards=Quicksort|Mergesort&size=%GG
/?algo=Quicksort&size=%GG&cards=Mergesort
```

**問題:**
`try-catch` 内の `foreach` 途中で例外が起きると `_initialCardAlgorithms` への代入がスキップされ、途中まで蓄積されたカード情報が消える。

**修正:** `parsedCards` を `try` 外に巻き上げ、`_initialCardAlgorithms = parsedCards.Distinct().ToList()` を `finally` ブロックに移動。例外発生時も途中まで蓄積されたカードを確定するよう修正済み。

---

### ℹ️ 2. `mode=PictureRow/Column/Block` + 画像未ロードで Generate（深刻度: 低、調査済み）

**入力例:**
```
/?mode=PictureRow&cards=Quicksort
```

**問題:**
URL 復元時に `RestoreCardsFromUrlAsync` がカードを追加して Generate まで走る。`_imageService.Current == null`（画像未ロード）の状態でソートアニメーションが開始される。

**調査結果（対応不要）:**
Canvas 2D パス（`pictureRowCanvasRenderer.js`）・Worker パス（`pictureRowRenderWorker.js`）ともに、画像が未ロードの場合は `if (img && numRows > 0 && img.complete)` / `if (imageBitmap && imageNumRows > 0)` の条件が false となり、カラーバー表示へ安全にフォールバックする。クラッシュなし。

---

### 3. `qs.Split('&')` のエントリ数自体に上限なし（深刻度: 低、事実上問題なし）

**入力例:** `/?a=1&a=1&a=1&...(数万ペア)`

**問題:** `Split('&')` は全量 `string[]` を確保してから `foreach` が走る。未知キーは `HashSet.Add`（O(1)）されるが全エントリ走査は避けられない。ブラウザの URL 長制限が現実的な防壁。据え置き。

---

### 4. `cards` 内 `Split` の全量アロケート（深刻度: 低、事実上問題なし）

**入力例:** `/?cards=A|A|A|A|...(数万個の | 区切り)`

**問題:** `val.Split('|', ...)` は `string[]` を全量確保してから `.Take` が適用される。URL 長制限が防壁のため実害軽微。据え置き。

---

### 5. `key` に `+` が含まれると非デコード（深刻度: 情報、事実上問題なし）

**問題:** `key` は `Uri.UnescapeDataString` のみで `+` のフォームデコードなし。`/?s+ize=1024` → `key = "s+ize"` → どの `case` にも一致せず無視。実害なし。据え置き。

---

## ホワイトリスト保護（現状の良い点）

- `algo`: `AlgorithmRegistry.GetAllAlgorithms()` 不一致 → 無視
- `pattern`: `ArrayPatternRegistry.GetAllPatterns()` 不一致 → 無視
- `mode`: `Enum.TryParse` 不一致 → 無視（ただし整数値は通過、問題1参照）
- `cards`: `GetAllAlgorithms()` 不一致 → `null` → `Where(n => n != null)` で除外
- XSS: 値は UI に直接レンダリングされず内部状態として使われるため、DOM インジェクションのリスクはない

---

## 優先度まとめ

| # | 場所 | 問題 | 深刻度 | 状態 |
|---|------|------|--------|------|
| 1 | `Index.razor` `size` | 負数・ゼロでクラッシュ | 高 | ✅ 修正済み |
| 2 | `Index.razor` `size` | 巨大値による DoS | 中 | ✅ 修正済み |
| 3 | `Index.razor` `cards` `\|` | 大量エントリで無駄な処理 | 低〜中 | ✅ 修正済み |
| 4 | `Index.razor` 重複キー | 後勝ちで上書き | 低 | ✅ 修正済み |
| 5 | `Index.razor` `+`/`%2B` | デコード混在の罠 | 低 | ℹ️ 据え置き（実害なし） |
| 6 | `Index.razor` `mode` | 未定義整数値で空白画面・URL汚染 | 中 | ✅ 修正済み |
| 7 | `Index.razor` `&cards=` 複数 | パラメータ累積に上限なし | 低〜中 | ✅ 修正済み |
| 8 | `Index.razor` `OnInitialized` | 不正 `%XX` で `UriFormatException` → ページクラッシュ | 低〜中 | ✅ 修正済み |
| 9 | `TutorialPage.razor` `AlgorithmName` | 不正エンコードで `UriFormatException` | 低〜中 | ✅ 修正済み |
| 10 | `Index.razor` `OnInitialized` | `UriFormatException` catch 後の部分的な状態適用（カード消失） | 低 | ✅ 修正済み |
| 11 | `PictureRowRenderer` | 画像未ロードで Generate 時の JS null safety | 低 | ℹ️ 調査済み（安全） |
| 12 | `Index.razor` `qs.Split` | エントリ数に上限なし | 低 | ℹ️ 据え置き（URL長で実害軽微） |
| 13 | `Index.razor` `cards` Split | 全量アロケート | 低 | ℹ️ 据え置き（URL長で実害軽微） |
| 14 | `Index.razor` `key` の `+` | デコードされない | 情報 | ℹ️ 据え置き（実害なし） |

---

## 推奨: 抜本的改善案

`System.Web.HttpUtility.ParseQueryString` は .NET 5 以降 Blazor WebAssembly でも追加パッケージなしで使用可能。標準的なデコード（`+` → スペース、`%XX` → 文字）と重複キー対応が一括で得られる。

```csharp
var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

var algoVal = query["algo"];
var sizeStr = query["size"];
if (algoVal != null) { ... }
if (sizeStr != null && int.TryParse(sizeStr, out var size) && size >= 1 && size <= 8192)
    _arraySize = size;
```

`ParseQueryString` は重複キーの値を `NameValueCollection` として保持し、`["key"]` は最後の値を返す。最初の値を優先したい場合は `query.GetValues("size")?[0]` を使う。

これにより手動パース由来の不具合クラス全体を排除できる。
