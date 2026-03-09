# PDQSort 実装比較: C++ vs Go vs 現在のC#実装

## 概要

PDQSort（Pattern-Defeating QuickSort）は、複数の言語で実装されており、それぞれ微妙に異なるアプローチを取っています。このドキュメントでは、C++オリジナル実装、Go標準ライブラリ実装、そして現在のC#実装の違いを詳しく解説します。

## 主要な違いの要約

| 機能 | C++実装 | Go実装 | 現在のC#実装 |
|------|---------|--------|--------------|
| Pivot選択後のSwap | 常に実行 | 条件付き | 常に実行 |
| ソート済み検出 | Partition後 | Partition前 | Partition後 |
| Pattern Breaking | Bad partition後 | Bad partition前 | Bad partition後 |
| 状態フラグ | なし | wasBalanced, wasPartitioned | なし |
| ソート済み配列のSwap回数 | 1回 | 0回（最適化可能） | 1回 |

## 1. Pivot選択とSwap処理の違い

### C++実装 (.references/pdqsort/pdqsort-master/pdqsort.h)

```cpp
// Line 414-422
diff_t s2 = size / 2;
if (size > ninther_threshold) {
    // Ninther: 9要素の中央値を計算
    sort3(begin, begin + s2, end - 1, comp);
    sort3(begin + 1, begin + (s2 - 1), end - 2, comp);
    sort3(begin + 2, begin + (s2 + 1), end - 3, comp);
    sort3(begin + (s2 - 1), begin + s2, begin + (s2 + 1), comp);
    std::iter_swap(begin, begin + s2);  // ← 常に実行
} else {
    sort3(begin + s2, begin, end - 1, comp);  // Median-of-3
}

// Line 435-437: Partitioning
std::pair<Iter, bool> part_result =
    Branchless ? partition_right_branchless(begin, end, comp)
               : partition_right(begin, end, comp);
```

**特徴:**
- Ninther計算後、**必ず `std::iter_swap(begin, begin + s2)` を実行**
- Partition関数は常に `*begin` をpivotとして使用
- ソート済み配列でも無条件に1回のSwapが発生

### Go実装 (.references/pdqsort/pdqsort-go/zsortfunc.go)

```go
// Line 89: Pivot選択（Swapなし！）
pivot, hint := choosePivot_func(data, a, b)

// Line 90-97: 逆順検出と反転
if hint == decreasingHint {
    reverseRange_func(data, a, b)
    pivot = (b - 1) - (pivot - a)
    hint = increasingHint
}

// Line 100-103: ソート済み検出（★重要★）
if wasBalanced && wasPartitioned && hint == increasingHint {
    if partialInsertionSort_func(data, a, b) {
        return  // ← Partitionせずに早期リターン！
    }
}

// Line 114: Partitioning（ここで初めてSwap）
mid, alreadyPartitioned := partition_func(data, a, b, pivot)
```

```go
// partition_func内部 (Line 136)
func partition_func(data lessSwap, a, b, pivot int) (newpivot int, alreadyPartitioned bool) {
    data.Swap(a, pivot)  // ← ここで初めてSwap
    // ... partitioning logic
}
```

**特徴:**
- `choosePivot_func` は**pivotインデックスを返すだけ**（Swapなし）
- ソート済み検出（`hint == increasingHint`）で**Partition前に早期リターン可能**
- 早期リターンすれば、**Swapは一度も実行されない**
- Partitionに到達した場合のみ、`partition_func`内で `data.Swap(a, pivot)` を実行

### 現在のC#実装 (src/SortAlgorithm/Algorithms/Partition/PDQSort.cs)

```csharp
// Line 208-233
var s2 = size / 2;
if (size > NintherThreshold)
{
    // Ninther: median of medians for better pivot selection
    Sort3(s, begin, begin + s2, end - 1);
    Sort3(s, begin + 1, begin + (s2 - 1), end - 2);
    Sort3(s, begin + 2, begin + (s2 + 1), end - 3);
    Sort3(s, begin + (s2 - 1), begin + s2, begin + (s2 + 1));
    s.Swap(begin, begin + s2);  // ← C++と同じく常に実行
}
else
{
    Sort3(s, begin + s2, begin, end - 1);
}

// Line 238-242: 等値要素の最適化
if (!leftmost && s.Compare(begin - 1, begin) >= 0)
{
    begin = PartitionLeft(s, begin, end) + 1;
    continue;
}

// Line 245: Partitioning
var (pivotPos, alreadyPartitioned) = PartitionRight(s, begin, end);
```

**特徴:**
- **C++実装と同じアプローチ**
- Ninther後、常に `s.Swap(begin, begin + s2)` を実行
- Partition関数は `s.Read(begin)` をpivotとして使用
- ソート済み配列でも1回のSwapが発生

## 2. ソート済み配列での動作比較

### ソート済み配列 `[1, 2, 3, ..., 200]` の場合

**C++実装:**
1. Ninther計算: `begin + s2` (= 100) に9要素の中央値（=100）が入る
2. `std::iter_swap(begin, begin + s2)` を実行
   - `array[0] = 100`, `array[100] = 1` になる（一時的に破壊）
3. Partition実行: `already_partitioned = true` を検出
4. Partial insertion sortで修正
5. **結果: Swap 1回**

**Go実装:**
1. `choosePivot_func` でpivotインデックス（=100）を返す
2. Swap回数が0なので `hint = increasingHint`
3. `wasBalanced && wasPartitioned && hint == increasingHint` が true
4. `partialInsertionSort_func` を実行
5. 既にソート済みなので早期リターン
6. **結果: Swap 0回**（`partition_func`に到達しない）

**現在のC#実装:**
1. Ninther計算: `begin + s2` (= 100) に9要素の中央値（=100）が入る
2. `s.Swap(begin, begin + s2)` を実行
3. Partition実行: `alreadyPartitioned = true` を検出
4. Partial insertion sortで修正
5. **結果: Swap 1回**

### 実験結果

```
=== ソート済み配列 (200要素) ===
Compares: 410
Swaps: 1         ← C++/C#実装では1回
IndexWrites: 4
```

Go実装では、ソート済み配列の場合、Swaps: 0になる可能性があります。

## 3. Pattern Breaking（パターン破壊）の違い

### C++実装

```cpp
// Line 253-288: Bad partition後にpattern breakingを実行
if (highlyUnbalanced)
{
    if (--badAllowed == 0)
    {
        HeapSort.SortCore(s, begin, end);
        return;
    }

    // Pattern breaking swaps
    if (lSize >= insertion_sort_threshold) {
        std::iter_swap(begin, begin + lSize / 4);
        std::iter_swap(pivotPos - 1, pivotPos - lSize / 4);
        // ...
    }
}
```

### Go実装

```go
// Line 84-87: Partition前にpattern breakingを実行
if !wasBalanced {
    breakPatterns_func(data, a, b)
    limit--
}

// その後、pivot選択とpartitioningを実行
```

**違い:**
- **C++:** Bad partition検出**後**にpattern breaking
- **Go:** 前回がbad partitionだった場合、**次のpartition前**にpattern breaking
- Goの方が予防的なアプローチ

## 4. 状態管理の違い

### C++実装

- ループ内で状態フラグなし
- 各iterationで独立して判断

### Go実装

```go
var (
    wasBalanced    = true  // 前回のpartitionが balanced だったか
    wasPartitioned = true  // 前回のpartitionが already partitioned だったか
)

for {
    // ...

    // 前回の状態に基づいて最適化
    if wasBalanced && wasPartitioned && hint == increasingHint {
        if partialInsertionSort_func(data, a, b) {
            return
        }
    }

    // Partition後に状態を更新
    mid, alreadyPartitioned := partition_func(data, a, b, pivot)
    wasPartitioned = alreadyPartitioned

    wasBalanced = leftLen >= balanceThreshold || rightLen >= balanceThreshold
}
```

**特徴:**
- `wasBalanced`: 前回のpartitionがバランスしていたかを追跡
- `wasPartitioned`: 前回のpartitionが既にソート済みだったかを追跡
- これらのフラグを組み合わせて、ソート済み検出の精度を向上

## 5. Go準拠への移行時の課題

### 実装上の複雑さ

1. **Pivot管理の変更**
   - C++: Pivotは常に `*begin` に配置
   - Go: Pivotはインデックスで管理、Partition時にSwap
   - → インデックスと値の混同を避ける必要

2. **状態フラグの導入**
   - `wasBalanced`, `wasPartitioned` の適切な管理
   - Recursion時の状態の引き継ぎ

3. **早期リターンのタイミング**
   - Partition前のソート済み検出
   - Partial insertion sortの成功/失敗の扱い

4. **比較タイミングの変更**
   - `s.Compare(begin - 1, pivot)` のpivotがインデックスか値か
   - Swap前後での値の変化を考慮

### 遭遇したエラー

実装中に `IndexOutOfRangeException` が多発:
- `MedianAdjacent` で `pos - 1`, `pos + 1` へのアクセス
- Pivot インデックスと値の混同
- 状態フラグの不適切な更新

## 6. 推奨される実装アプローチ

### オプション1: C++準拠（現状）

**メリット:**
- シンプルで理解しやすい
- 既存のC++実装と一対一対応
- デバッグが容易

**デメリット:**
- ソート済み配列でも1回の無駄なSwap
- Pattern breakingが事後的

### オプション2: Go準拠（フル実装）

**メリット:**
- ソート済み配列でSwap 0回（最適化）
- 予防的なpattern breaking
- より洗練されたアルゴリズム

**デメリット:**
- 実装が複雑
- デバッグが困難
- 状態管理のオーバーヘッド

### オプション3: ハイブリッドアプローチ（推奨）

段階的に最適化を追加:

1. **Phase 1: Conditional Swap**
   ```csharp
   // Ninther後
   if (begin != begin + s2) {
       s.Swap(begin, begin + s2);
   }
   ```
   → しかし、この最適化は**効果なし**（常に `begin != begin + s2`）

2. **Phase 2: Early Detection**
   ```csharp
   // Pivot選択時にhintを計算
   var (pivot, hint) = ChoosePivot(s, begin, end);

   // ソート済み検出
   if (hint == SortedHint.Increasing && alreadyPartitionedLastTime) {
       if (PartialInsertionSort(s, begin, end)) {
           return;
       }
   }
   ```

3. **Phase 3: 状態フラグの導入**
   - `wasBalanced`, `wasPartitioned` を追加
   - Partition前のソート済み検出

## 7. 実装例: Go準拠のコア部分

### ChoosePivot関数（値ではなくインデックスを返す）

```csharp
private enum SortedHint
{
    Unknown = 0,
    Increasing = 1,
    Decreasing = 2
}

private static (int pivot, SortedHint hint) ChoosePivot<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end) where TComparer : IComparer<T>
{
    const int MaxSwaps = 4 * 3;
    var size = end - begin;
    var swaps = 0;

    int i = begin + size / 4;
    int j = begin + size / 2;
    int k = begin + (size / 4) * 3;

    if (size >= 8)
    {
        if (size > NintherThreshold)
        {
            // Tukey ninther method
            i = MedianAdjacent(s, i, ref swaps);
            j = MedianAdjacent(s, j, ref swaps);
            k = MedianAdjacent(s, k, ref swaps);
        }
        // Find median among i, j, k
        Sort3WithSwapCount(s, i, j, k, ref swaps);
    }

    var hint = swaps switch
    {
        0 => SortedHint.Increasing,
        MaxSwaps => SortedHint.Decreasing,
        _ => SortedHint.Unknown
    };

    return (j, hint);  // インデックスを返す
}
```

### メインループ（Go準拠）

```csharp
private static void PDQSortLoop<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end, int badAllowed,
    bool leftmost, ISortContext context) where TComparer : IComparer<T>
{
    var wasBalanced = true;
    var wasPartitioned = true;

    while (true)
    {
        var size = end - begin;

        if (size < InsertionSortThreshold) {
            // Insertion sort
            return;
        }

        // Pattern breaking（前回がunbalancedだった場合）
        if (!wasBalanced) {
            BreakPatterns(s, begin, end);
            badAllowed--;
        }

        // Pivot選択（Swapなし）
        var (pivot, hint) = ChoosePivot(s, begin, end);

        // 逆順検出
        if (hint == SortedHint.Decreasing) {
            ReverseRange(s, begin, end);
            pivot = (end - 1) - (pivot - begin);
            hint = SortedHint.Increasing;
        }

        // ソート済み検出（Partition前）
        if (wasBalanced && wasPartitioned && hint == SortedHint.Increasing) {
            if (PartialInsertionSort(s, begin, end)) {
                return;  // Swapなしで完了！
            }
        }

        // 等値要素の最適化
        if (!leftmost && s.Compare(begin - 1, s.Read(pivot)) >= 0) {
            begin = PartitionLeft(s, begin, end, pivot) + 1;
            continue;
        }

        // Partitioning（ここで初めてSwap）
        var (pivotPos, alreadyPartitioned) = PartitionRight(s, begin, end, pivot);
        wasPartitioned = alreadyPartitioned;

        // Balance チェック
        var lSize = pivotPos - begin;
        var rSize = end - (pivotPos + 1);
        var balanceThreshold = size / 8;

        if (lSize < balanceThreshold || rSize < balanceThreshold) {
            wasBalanced = false;
            if (badAllowed == 0) {
                HeapSort.SortCore(s, begin, end);
                return;
            }
        } else {
            wasBalanced = true;
        }

        // Tail recursion
        if (lSize < rSize) {
            PDQSortLoop(s, begin, pivotPos, badAllowed, leftmost, context);
            begin = pivotPos + 1;
        } else {
            PDQSortLoop(s, pivotPos + 1, end, badAllowed, false, context);
            end = pivotPos;
        }
        leftmost = false;
    }
}
```

### PartitionRight（pivotインデックスを受け取る）

```csharp
private static (int pivotPos, bool alreadyPartitioned) PartitionRight<T, TComparer>(
    SortSpan<T, TComparer> s, int begin, int end, int pivotIndex) where TComparer : IComparer<T>
{
    // Pivotをbeginに移動（Go実装と同じ）
    s.Swap(begin, pivotIndex);

    // Pivot値を読み取り
    var pivot = s.Read(begin);

    // ... 既存のpartitioning logic
}
```

## 8. パフォーマンス比較

### 理論的な違い

| シナリオ | C++/C# | Go |
|---------|--------|-----|
| ソート済み配列 | Swap 1回 + Partial IS | Swap 0回 + Partial IS |
| 逆順配列 | Reverse + 通常処理 | Reverse + 通常処理 |
| ランダム配列 | 同等 | 同等 |
| Bad partition | Pattern breaking後 | Pattern breaking前 |

### 実測値（予想）

ソート済み配列（200要素）:
- **C++/C# 実装:** Compares: ~410, Swaps: 1, Writes: ~4
- **Go 実装:** Compares: ~410, Swaps: 0, Writes: ~2

## 9. まとめ

### 現在のC#実装の評価

- ✅ C++実装に忠実
- ✅ シンプルで理解しやすい
- ✅ 全テストパス
- ⚠️ ソート済み配列で1回の無駄なSwap

### Go準拠への移行の評価

- ✅ ソート済み配列でSwap削減
- ✅ より洗練されたアルゴリズム
- ❌ 実装が複雑
- ❌ デバッグが困難
- ❌ 段階的な移行が必要

### 推奨

**短期的:** 現在のC++準拠実装を維持
- 理由: シンプルで安定、1回のSwapは許容範囲

**中期的:** ハイブリッドアプローチ
- Phase 1: `ChoosePivot` 関数の分離（インデックスベース）
- Phase 2: 状態フラグの導入
- Phase 3: Partition前のソート済み検出

**長期的:** 完全なGo準拠実装
- 十分なテストとベンチマークを実施後

## 10. 参考リンク

- **C++実装:** https://github.com/orlp/pdqsort
- **Go実装:** Go標準ライブラリ (`sort` package)
- **論文:** Pattern-Defeating Quicksort (https://arxiv.org/abs/2106.05123)

---

**作成日:** 2026-01-02
**作成者:** GitHub Copilot Analysis
**対象バージョン:** C# (.NET 10), C++ (orlp/pdqsort), Go 1.19+
