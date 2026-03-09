# Release ビルドでの統計計測を可能にする設計

## 問題の定義

### 現状の課題

現在、`SortSpan<T, TComparer>` は `#if DEBUG` ディレクティブを使用しており、以下の制約があります:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public T Read(int i)
{
#if DEBUG
    _context.OnIndexRead(i, _bufferId);
#endif
    return _span[i];
}
```

**問題点:**

1. **Web ビジュアライゼーションは Debug ビルドが必須**
   - ソート操作の観測には `SortSpan` が各種コンテキストをフックする必要がある
   - フックは Debug ビルドでのみ有効化されている
   - 結果: Web アプリは Debug ビルドで実行せざるを得ない

2. **Debug ビルドでは正確な性能測定ができない**
   - JIT の最適化が制限される
   - インライン化が抑制される
   - 余分なチェックが追加される
   - 結果: 実行時間が Release ビルドの数倍～数十倍遅い

3. **Release ビルドと Debug ビルドで二者択一**
   - 観測 (統計・可視化) → Debug ビルド必須 → 遅い
   - 性能測定 → Release ビルド必須 → 観測不可
   - **両立できない**

### ビジネス要求

- **Web ビジュアライゼーションを Release ビルドで実行したい**
  - ユーザー体験の向上（応答性）
  - 正確な実行時間の表示
  - プロダクション品質のパフォーマンス

- **統計計測は維持したい**
  - Compare/Swap/IndexRead/IndexWrite のカウント
  - 可視化のためのイベントフック

---

## 解決策の検討

### 案1: 型パラメータによる最適化パス（推奨） ← 採用

#### 概要

`SortSpan` に `TContext` 型パラメータを追加し、JIT の型特殊化を活用します。

```csharp
internal readonly ref struct SortSpan<T, TComparer, TContext> 
    where TComparer : IComparer<T>
    where TContext : ISortContext
{
    private readonly Span<T> _span;
    private readonly TContext _context;
    private readonly TComparer _comparer;
    private readonly int _bufferId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Read(int i)
    {
        // typeof(TContext) はコンパイル時定数
        if (typeof(TContext) != typeof(NullContext))
        {
            _context.OnIndexRead(i, _bufferId);
        }
        return _span[i];
    }
}
```

#### JIT の挙動

.NET の JIT コンパイラは、ジェネリック型ごとに専用のネイティブコードを生成します（特に struct 型パラメータの場合）。

**`NullContext` を使用した場合:**

```csharp
// ソースコード
if (typeof(TContext) != typeof(NullContext))
{
    _context.OnIndexRead(i, _bufferId);
}

// JIT による最適化後（NullContext の場合）
if (false)  // コンパイル時定数として評価
{
    // このブロックは完全に削除される（Dead Code Elimination）
}

// 最終的な生成コード
return _span[i];  // 1命令のみ
```

**`StatisticsContext` を使用した場合:**

```csharp
// JIT による最適化後（StatisticsContext の場合）
if (true)  // コンパイル時定数として評価
{
    _context.OnIndexRead(i, _bufferId);
}
return _span[i];

// 最終的な生成コード
_context.OnIndexRead(i, _bufferId);
return _span[i];
```

#### メリット

✅ **ランタイムオーバーヘッド完全ゼロ**
- `NullContext` 使用時は条件分岐すら生成されない
- Release ビルドの最適化と完全に同等のパフォーマンス

✅ **型安全性**
- コンパイル時に型が確定
- 間違ったコンテキストの使用を防げる

✅ **Debug/Release の区別が不要**
- `#if DEBUG` ディレクティブを完全削除
- どちらのビルドでも同じコードパス

✅ **柔軟性**
- `NullContext`: 最速パス（性能測定用）
- `StatisticsContext`: 統計収集
- `VisualizationContext`: 可視化イベント
- カスタムコンテキスト: ユーザー定義の観測

#### デメリット

⚠️ **API の破壊的変更**
- `SortSpan<T, TComparer>` → `SortSpan<T, TComparer, TContext>`
- すべてのソートアルゴリズムのシグネチャ変更が必要
- 既存のテストコードの更新が必要

⚠️ **コードの複雑性が若干増加**
- 型パラメータが1つ増える
- 初見の開発者には理解コストがかかる可能性

⚠️ **コンパイル時間の増加**
- 型の組み合わせごとにコード生成
- ただし、実際の影響は微小（型の数は限定的）

#### 実装の影響範囲

**変更が必要なファイル:**

1. `src/SortAlgorithm/Algorithms/SortSpan.cs`
   - 型パラメータ `TContext` を追加
   - `#if DEBUG` を `if (typeof(TContext) != typeof(NullContext))` に置換

2. すべてのソートアルゴリズム（約20ファイル）
   - `Sort<T>` メソッドのシグネチャに `TContext` を追加
   - ヘルパーメソッドの型パラメータを更新

3. すべてのテストコード
   - コンテキストの明示的な指定を追加
   - デフォルトは `NullContext` または `StatisticsContext`

4. Web ビジュアライゼーション
   - コンテキストの型パラメータを明示的に指定

#### 後方互換性の戦略

デフォルト型パラメータは C# では使用できないため、オーバーロードで対応:

```csharp
public static class BubbleSort
{
    // 便利メソッド: NullContext をデフォルト使用
    public static void Sort<T>(Span<T> span)
        where T : IComparable<T>
    {
        Sort<T, ComparableComparer<T>, NullContext>(span, new ComparableComparer<T>(), NullContext.Default);
    }

    // 統計計測版
    public static void Sort<T>(Span<T> span, StatisticsContext context)
        where T : IComparable<T>
    {
        Sort<T, ComparableComparer<T>, StatisticsContext>(span, new ComparableComparer<T>(), context);
    }

    // フルコントロール版（内部使用）
    internal static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var sortSpan = new SortSpan<T, TComparer, TContext>(span, context, comparer, 0);
        // ...
    }
}
```

---

### 案2: マーカーインターフェイスによる分岐

#### 概要

```csharp
public interface IOptimizedContext : ISortContext
{
    // マーカーインターフェイス（メソッドなし）
}

public sealed class NullContext : IOptimizedContext
{
    // ...
}

internal readonly ref struct SortSpan<T, TComparer>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Read(int i)
    {
        if (_context is not IOptimizedContext)
        {
            _context.OnIndexRead(i, _bufferId);
        }
        return _span[i];
    }
}
```

#### メリット

✅ 既存の API シグネチャを維持
✅ 実装が比較的簡単

#### デメリット

❌ **毎回のランタイムチェック**
- `is` チェックのオーバーヘッド（数ナノ秒）
- インライン化が阻害される可能性

❌ **JIT の最適化が効きにくい**
- 型が実行時まで確定しない
- デッドコード削除ができない

❌ **パフォーマンスの劣化**
- 案1 と比較して 5-10% 程度遅くなる可能性

**評価: 性能要件を満たさないため不採用**

---

### 案3: 静的ファクトリパターン

#### 概要

```csharp
internal static class SortSpan
{
    public static SortSpan<T, TComparer> Create<T, TComparer>(
        Span<T> span, ISortContext context, TComparer comparer)
    {
        if (context is NullContext)
            return new OptimizedSortSpan<T, TComparer>(span, comparer);
        else
            return new TrackedSortSpan<T, TComparer>(span, context, comparer);
    }
}
```

#### デメリット

❌ **ref struct は継承不可**
- 共通の基底型を作れない
- インターフェイスも実装できない（C# 11 以前）

❌ **実装の重複**
- `OptimizedSortSpan` と `TrackedSortSpan` で同じコードを2回書く必要

**評価: 技術的制約により不採用**

---

### 案4: Source Generator による自動生成

#### 概要

Source Generator で最適化版と計測版の2つの実装を自動生成

#### デメリット

❌ **複雑性の爆発**
❌ **デバッグの困難さ**
❌ **ビルド時間の増加**

**評価: オーバーエンジニアリングのため不採用**

---

## 採用案: 案1（型パラメータアプローチ）

### 設計の詳細

#### 1. `SortSpan` の新しいシグネチャ

```csharp
internal readonly ref struct SortSpan<T, TComparer, TContext> 
    where TComparer : IComparer<T>
    where TContext : ISortContext
{
    private readonly Span<T> _span;
    private readonly TContext _context;
    private readonly TComparer _comparer;
    private readonly int _bufferId;

    public SortSpan(Span<T> span, TContext context, TComparer comparer, int bufferId)
    {
        _span = span;
        _context = context;
        _comparer = comparer;
        _bufferId = bufferId;
    }

    // 各メソッドで最適化分岐を実装
}
```

#### 2. 最適化条件の実装パターン

すべての操作で統一的なパターンを使用:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public T Read(int i)
{
    if (typeof(TContext) != typeof(NullContext))
    {
        _context.OnIndexRead(i, _bufferId);
    }
    return _span[i];
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void Write(int i, T value)
{
    if (typeof(TContext) != typeof(NullContext))
    {
        _context.OnIndexWrite(i, _bufferId, value);
    }
    _span[i] = value;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public int Compare(int i, int j)
{
    if (typeof(TContext) != typeof(NullContext))
    {
        var a = Read(i);
        var b = Read(j);
        var result = _comparer.Compare(a, b);
        _context.OnCompare(i, j, result, _bufferId, _bufferId);
        return result;
    }
    else
    {
        // NullContext の場合は直接アクセス
        return _comparer.Compare(_span[i], _span[j]);
    }
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void Swap(int i, int j)
{
    if (typeof(TContext) != typeof(NullContext))
    {
        _context.OnSwap(i, j, _bufferId);
    }
    (_span[i], _span[j]) = (_span[j], _span[i]);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void CopyTo(int sourceIndex, SortSpan<T, TComparer, TContext> destination, int destinationIndex, int length)
{
    if (typeof(TContext) != typeof(NullContext))
    {
        var values = new object?[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = _span[sourceIndex + i];
        }
        _context.OnRangeCopy(sourceIndex, destinationIndex, length, _bufferId, destination.BufferId, values);
    }
    _span.Slice(sourceIndex, length).CopyTo(destination._span.Slice(destinationIndex, length));
}
```

#### 3. ソートアルゴリズムの更新パターン

**変更前:**

```csharp
public static class BubbleSort
{
    public static void Sort<T>(Span<T> span)
        where T : IComparable<T>
    {
        Sort(span, new ComparableComparer<T>());
    }

    public static void Sort<T, TComparer>(Span<T> span, TComparer comparer)
        where TComparer : IComparer<T>
    {
        Sort(span, comparer, NullContext.Default);
    }

    internal static void Sort<T, TComparer>(Span<T> span, TComparer comparer, ISortContext context)
        where TComparer : IComparer<T>
    {
        var sortSpan = new SortSpan<T, TComparer>(span, context, comparer, 0);
        // ...
    }
}
```

**変更後:**

```csharp
public static class BubbleSort
{
    // 便利メソッド: NullContext で高速実行
    public static void Sort<T>(Span<T> span)
        where T : IComparable<T>
    {
        Sort<T, ComparableComparer<T>, NullContext>(span, new ComparableComparer<T>(), NullContext.Default);
    }

    // 便利メソッド: カスタムコンパラ + NullContext
    public static void Sort<T, TComparer>(Span<T> span, TComparer comparer)
        where TComparer : IComparer<T>
    {
        Sort<T, TComparer, NullContext>(span, comparer, NullContext.Default);
    }

    // 統計計測用オーバーロード
    public static void Sort<T>(Span<T> span, StatisticsContext context)
        where T : IComparable<T>
    {
        Sort<T, ComparableComparer<T>, StatisticsContext>(span, new ComparableComparer<T>(), context);
    }

    // フルコントロール版（内部 + テスト用）
    public static void Sort<T, TComparer, TContext>(Span<T> span, TComparer comparer, TContext context)
        where TComparer : IComparer<T>
        where TContext : ISortContext
    {
        var sortSpan = new SortSpan<T, TComparer, TContext>(span, context, comparer, 0);
        // ...（既存のロジックは変更不要）
    }
}
```

#### 4. Web ビジュアライゼーションでの使用

**シナリオ1: 統計収集 + 可視化イベント**

```csharp
var visualizationContext = new VisualizationContext();
BubbleSort.Sort<int, Comparer<int>, VisualizationContext>(
    data.AsSpan(), 
    Comparer<int>.Default, 
    visualizationContext);
```

**シナリオ2: 純粋な実行時間測定（統計不要）**

```csharp
var stopwatch = Stopwatch.StartNew();
BubbleSort.Sort<int, Comparer<int>, NullContext>(
    data.AsSpan(), 
    Comparer<int>.Default, 
    NullContext.Default);
stopwatch.Stop();
// → Release ビルドと同等のパフォーマンス
```

---

## 実装計画

### フェーズ1: コア変更（SortSpan）

**タスク:**
1. `SortSpan<T, TComparer, TContext>` の型パラメータ追加
2. すべてのメソッドで最適化条件を実装
3. `#if DEBUG` ディレクティブの削除
4. 単体テストの作成（NullContext/StatisticsContext での動作確認）

**期待される結果:**
- NullContext 使用時のパフォーマンスが Release ビルドと同等
- StatisticsContext 使用時に正確な統計が収集される

**検証方法:**
```csharp
// パフォーマンステスト
var data = Enumerable.Range(0, 10000).ToArray();
var stopwatch = Stopwatch.StartNew();
BubbleSort.Sort<int, Comparer<int>, NullContext>(data.AsSpan(), Comparer<int>.Default, NullContext.Default);
stopwatch.Stop();
// → 既存の Release ビルドと比較して ±5% 以内

// 統計テスト
var stats = new StatisticsContext();
BubbleSort.Sort<int, Comparer<int>, StatisticsContext>(data.AsSpan(), Comparer<int>.Default, stats);
Assert.True(stats.CompareCount > 0);
```

### フェーズ2: ソートアルゴリズムの更新

**対象ファイル（例）:**
- `BubbleSort.cs`
- `InsertionSort.cs`
- `QuickSort.cs`
- `MergeSort.cs`
- `HeapSort.cs`
- 他すべてのアルゴリズム（約20ファイル）

**タスク:**
1. 各アルゴリズムに型パラメータ `TContext` を追加
2. オーバーロードメソッドの実装（便利メソッド）
3. 内部ヘルパーメソッドの型パラメータ更新

**変更パターン（一括置換可能）:**
```csharp
// 置換前
internal static void Sort<T, TComparer>(...)
    where TComparer : IComparer<T>
{
    var sortSpan = new SortSpan<T, TComparer>(...);
}

// 置換後
public static void Sort<T, TComparer, TContext>(...)
    where TComparer : IComparer<T>
    where TContext : ISortContext
{
    var sortSpan = new SortSpan<T, TComparer, TContext>(...);
}
```

### フェーズ3: テストコードの更新

**対象:**
- `tests/SortAlgorithm.Tests/` 配下のすべてのテストファイル

**タスク:**
1. `NullContext.Default` または `new StatisticsContext()` を明示的に渡す
2. 統計検証テストで `StatisticsContext` を使用
3. パフォーマンステストで `NullContext` を使用

**例:**
```csharp
// 更新前
[Fact]
public void Sort_RandomArray_SortsCorrectly()
{
    var data = new[] { 5, 2, 8, 1, 9 };
    BubbleSort.Sort(data.AsSpan());
    Assert.True(data.SequenceEqual(new[] { 1, 2, 5, 8, 9 }));
}

// 更新後（API が自動的に NullContext を使用）
[Fact]
public void Sort_RandomArray_SortsCorrectly()
{
    var data = new[] { 5, 2, 8, 1, 9 };
    BubbleSort.Sort(data.AsSpan());  // 内部で NullContext を使用
    Assert.True(data.SequenceEqual(new[] { 1, 2, 5, 8, 9 }));
}

// 統計検証テスト
[Fact]
public void Sort_TracksStatistics()
{
    var data = new[] { 5, 2, 8, 1, 9 };
    var stats = new StatisticsContext();
    BubbleSort.Sort(data.AsSpan(), stats);
    Assert.True(stats.CompareCount > 0);
    Assert.True(stats.SwapCount > 0);
}
```

### フェーズ4: Web ビジュアライゼーションの更新

**対象ファイル:**
- `SortExecutor.cs`
- `AlgorithmRegistry.cs`
- 関連するサービスクラス

**タスク:**
1. Release ビルドへの切り替え
2. コンテキストの型パラメータを明示的に指定
3. パフォーマンスモードとビジュアライゼーションモードの切り替え実装

**例:**
```csharp
// VisualizationMode: イベント収集
var visualizationContext = new VisualizationContext();
algorithm.Sort<int, Comparer<int>, VisualizationContext>(
    data, Comparer<int>.Default, visualizationContext);

// PerformanceMode: 最速実行
var stopwatch = Stopwatch.StartNew();
algorithm.Sort<int, Comparer<int>, NullContext>(
    data, Comparer<int>.Default, NullContext.Default);
stopwatch.Stop();
```

### フェーズ5: ドキュメント更新

**タスク:**
1. `.github/agent_docs/sortspan_usage.md` の更新
2. `.github/agent_docs/implementation_template.md` の更新
3. `.github/copilot-instructions.md` の更新
4. このドキュメント（`Release_ButStatistics.md`）の完成版作成

---

## パフォーマンスへの期待値

### NullContext 使用時（最速パス）

**期待される動作:**
- JIT が `if (typeof(TContext) != typeof(NullContext))` を `if (false)` として評価
- コンテキスト呼び出しが完全に削除される（Dead Code Elimination）
- Release ビルドと同等のネイティブコード生成

**ベンチマーク予測:**

| ビルド構成 | コンテキスト | 実行時間（相対値） |
|-----------|------------|------------------|
| Debug | StatisticsContext | 1000% (基準) |
| Debug | NullContext | 950% |
| Release (#if DEBUG) | - | 100% |
| **Release (新実装)** | **NullContext** | **100-105%** ✅ |
| Release (新実装) | StatisticsContext | 120-150% |

**目標:**
- NullContext 使用時は現在の Release ビルドの ±5% 以内
- StatisticsContext 使用時でも Debug ビルドより大幅に高速

### StatisticsContext 使用時（計測パス）

**オーバーヘッド:**
- コンテキスト呼び出し: 数ナノ秒/回
- Interlocked.Increment: ~10ns
- 小規模配列（n < 1000）: 20-50% のオーバーヘッド
- 大規模配列（n > 10000）: 10-20% のオーバーヘッド（アルゴリズムの計算量が支配的）

**許容範囲:**
- 統計収集は正確性が最優先
- パフォーマンスは副次的
- Debug ビルドよりは高速であれば十分

---

## リスクと対策

### リスク1: JIT の最適化が期待通りに動作しない

**発生確率:** 低（.NET の既知の最適化パターン）

**対策:**
1. ベンチマークで検証
2. SharpLab や BenchmarkDotNet の Disassembly 機能で生成コードを確認
3. 必要に応じて `[MethodImpl(MethodImplOptions.AggressiveOptimization)]` を追加

### リスク2: すべてのアルゴリズムの更新に時間がかかる

**発生確率:** 中

**対策:**
1. 機械的な置換が可能な部分はスクリプトまたは一括置換
2. 段階的なマージ（アルゴリズムごとにPR分割）
3. テストで回帰を確認しながら進める

### リスク3: API の破壊的変更によるユーザーへの影響

**発生確率:** 低（内部ライブラリのため）

**対策:**
1. オーバーロードで既存の呼び出しパターンを維持
2. 破壊的変更は内部 API に限定
3. パブリック API は便利メソッドで互換性を保つ

---

## 成功基準

### 機能要件
- ✅ Release ビルドで `NullContext` を使用した場合、統計フックが無効化される
- ✅ Release ビルドで `StatisticsContext` を使用した場合、正確な統計が収集される
- ✅ すべての既存テストがパスする

### 非機能要件
- ✅ `NullContext` 使用時のパフォーマンスが既存の Release ビルドの ±5% 以内
- ✅ Web ビジュアライゼーションが Release ビルドで動作する
- ✅ ビルド時間が 20% 以上増加しない

### 品質要件
- ✅ コードカバレッジが低下しない
- ✅ すべてのアルゴリズムで一貫したパターンを使用
- ✅ ドキュメントが最新の状態に更新される

---

## 代替案との比較まとめ

| 項目 | 案1: 型パラメータ | 案2: マーカーIF | 案3: ファクトリ | 案4: Source Gen |
|------|-----------------|----------------|----------------|-----------------|
| ランタイムオーバーヘッド | **ゼロ** ✅ | 5-10% ⚠️ | N/A | ゼロ |
| 実装の複雑性 | 中 | 低 | 高 | 非常に高 |
| API の変更 | 大（型パラメータ追加） | 小 | 中 | 小 |
| JIT 最適化 | **最適** ✅ | 部分的 ⚠️ | N/A | 最適 |
| 保守性 | 良好 | 良好 | 低い | 低い |
| 技術的実現性 | **高** ✅ | 高 | **不可** ❌ | 中 |
| **総合評価** | **採用** ✅ | 不採用 | 不採用 | 不採用 |

---

## 結論

**型パラメータアプローチ（案1）を採用します。**

この設計により、以下が実現されます:

1. **Release ビルドで Web アプリを実行可能**
   - NullContext で最速パス
   - ユーザーに正確な実行時間を提示

2. **統計計測も維持**
   - StatisticsContext で正確な統計
   - 可視化コンテキストも引き続き使用可能

3. **ゼロオーバーヘッドの最適化**
   - JIT の型特殊化を活用
   - デッドコード削除により完全な最適化

4. **柔軟性の向上**
   - カスタムコンテキストの実装が容易
   - 用途に応じた最適なコンテキストを選択可能

実装の詳細は各フェーズの計画に従い、段階的に進めます。
