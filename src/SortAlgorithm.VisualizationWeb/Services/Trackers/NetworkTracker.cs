using SortAlgorithm.VisualizationWeb.Models;

namespace SortAlgorithm.VisualizationWeb.Services;

/// <summary>
/// SortingNetwork ビジュアライゼーション用トラッカー。
/// Bitonic sort のような比較ネットワークの構造を事前計算し、
/// 各ステップでコンパレータの処理状態を追跡する。
/// </summary>
sealed class NetworkTracker : IVisualizationTracker
{
    private readonly int _wireCount;
    private readonly NetworkComparator[] _comparators;
    private readonly bool?[] _comparatorResults;
    private int _currentComparatorIndex = 0;
    private int _activeComparatorIndex = -1;
    private bool _pendingSwap = false;
    private (int, int)? _pendingCompare = null;

    // Decorate() 用キャッシュ
    private NetworkSnapshot? _cachedSnapshot;

    internal NetworkTracker(int arrayLength)
    {
        _wireCount = arrayLength;
        _comparators = GenerateBitonicNetwork(arrayLength);
        _comparatorResults = new bool?[_comparators.Length];
    }

    /// <summary>
    /// Bitonic sort のネットワーク構造を生成する。
    /// n = 8 の場合、24 個のコンパレータ、6 ステージが生成される。
    /// </summary>
    private static NetworkComparator[] GenerateBitonicNetwork(int n)
    {
        var comparators = new List<NetworkComparator>();
        int stage = 0;

        // Bitonic sort: ビルドフェーズとマージフェーズ
        for (int k = 2; k <= n; k *= 2) // k: 比較間隔（2, 4, 8）
        {
            for (int j = k / 2; j > 0; j /= 2) // j: マージ幅の半分
            {
                for (int i = 0; i < n; i++)
                {
                    int ij = i ^ j;
                    if (ij > i)
                    {
                        bool ascending = (i & k) == 0;
                        // ソート方向によって wire1/wire2 を決定
                        // ascending なら小さい方を上、descending なら大きい方を上
                        comparators.Add(new NetworkComparator
                        {
                            Wire1 = i,
                            Wire2 = ij,
                            Stage = stage
                        });
                    }
                }
                stage++;
            }
        }

        return comparators.ToArray();
    }

    public void Process(SortOperation op, int[] mainArray, Dictionary<int, int[]> buffers)
    {
        if (op.BufferId1 != 0) return;

        switch (op.Type)
        {
            case OperationType.Compare:
            {
                // Compare が来たら次のコンパレータをアクティブ化
                if (_currentComparatorIndex < _comparators.Length)
                {
                    var comp = _comparators[_currentComparatorIndex];
                    // Compare のインデックスがコンパレータと一致するか確認
                    if ((op.Index1 == comp.Wire1 && op.Index2 == comp.Wire2) ||
                        (op.Index1 == comp.Wire2 && op.Index2 == comp.Wire1))
                    {
                        _activeComparatorIndex = _currentComparatorIndex;
                        _pendingCompare = (comp.Wire1, comp.Wire2);
                    }
                }
                break;
            }

            case OperationType.Swap:
            {
                // Swap が来た場合、直前の Compare に対応するコンパレータの結果を true に
                if (_pendingCompare.HasValue &&
                    ((op.Index1 == _pendingCompare.Value.Item1 && op.Index2 == _pendingCompare.Value.Item2) ||
                     (op.Index1 == _pendingCompare.Value.Item2 && op.Index2 == _pendingCompare.Value.Item1)))
                {
                    if (_currentComparatorIndex < _comparators.Length)
                    {
                        _comparatorResults[_currentComparatorIndex] = true;
                        _currentComparatorIndex++;
                        _pendingCompare = null;
                    }
                }
                break;
            }
        }

        // Snapshot を作成
        _cachedSnapshot = new NetworkSnapshot
        {
            WireCount = _wireCount,
            Comparators = _comparators,
            ActiveComparatorIndex = _activeComparatorIndex,
            ComparatorResults = (bool?[])_comparatorResults.Clone(),
            WireValues = (int[])mainArray.Clone()
        };
    }

    public TutorialStep Decorate(TutorialStep step)
    {
        // Compare の直後で Swap がない場合、コンパレータの結果を false に設定
        if (_pendingCompare.HasValue && step.HighlightType == OperationType.Compare)
        {
            // 次の操作が Swap でない場合は false
            if (_currentComparatorIndex < _comparators.Length)
            {
                // この時点では Swap がまだ来ていないので保留
                // PostStep で処理する
            }
        }

        return step with { Network = _cachedSnapshot };
    }

    public void PostStep()
    {
        // Compare の直後で Swap がなかった場合、コンパレータの結果を false に設定
        if (_pendingCompare.HasValue)
        {
            if (_currentComparatorIndex < _comparators.Length)
            {
                _comparatorResults[_currentComparatorIndex] = false;
                _currentComparatorIndex++;
            }
            _pendingCompare = null;
            _activeComparatorIndex = -1;
        }
    }
}
