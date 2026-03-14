using SortVivo.Models;

namespace SortVivo.Services;

/// <summary>
/// FlashSort チュートリアル用トラッカー。
/// <br/>
/// アルゴリズム側からの通知ループを不要にするため、コンストラクタで
/// <paramref name="initialArray"/> から FlashSort と同一のロジックで
/// boundary[]/counts[] を事前計算する。
/// これにより <c>FlashSort.cs</c> 本体には可視化固有のコードを一切含めない。
/// <br/>
/// FlashSort は置換を in-place で行うためバケットリスト（Buckets[]）は空のまま。
/// クラスカウントヒストグラムと置換先インデックスのアクティブクラスハイライトで可視化する。
/// </summary>
sealed class FlashSortTracker : IVisualizationTracker
{
    // FlashSort.cs の private 定数と同値
    private const int InsertionSortThreshold = 16;

    private readonly int _totalClasses;
    private readonly int[]? _boundaries;  // boundary[k] = exclusive upper bound of class k
    private readonly int[]? _counts;      // counts[k]   = number of elements in class k
    private readonly string[]? _labels;

    private DistributionPhase _phase = DistributionPhase.Count;
    private int _activeBucket = -1;
    // prefix sum が完了するまでヒストグラムを表示しない
    private bool _countsReady;

    internal FlashSortTracker(int[] initialArray)
    {
        var n = initialArray.Length;
        if (n <= InsertionSortThreshold) return; // FlashSort がそのまま InsertionSort に委譲する場合

        var m = Math.Max(2, (int)(0.43 * n));

        // FlashSort と同一の符号なしキー変換（int 型限定: 符号ビット反転）
        ulong minKey = ulong.MaxValue;
        ulong maxKey = ulong.MinValue;
        foreach (var v in initialArray)
        {
            var key = ToUnsignedKey(v);
            if (key < minKey) minKey = key;
            if (key > maxKey) maxKey = key;
        }

        if (minKey == maxKey) return; // 全要素が同値 → FlashSort は早期リターン

        var range = maxKey - minKey;

        var counts = new int[m];
        foreach (var v in initialArray)
        {
            var key = ToUnsignedKey(v);
            counts[ClassOf(key, minKey, range, m)]++;
        }

        // prefix sum → boundary[k] = exclusive upper bound of class k
        var boundaries = new int[m];
        boundaries[0] = counts[0];
        for (var k = 1; k < m; k++)
            boundaries[k] = boundaries[k - 1] + counts[k];

        _totalClasses = m;
        _boundaries = boundaries;
        _counts = counts;
        _labels = Enumerable.Range(0, m).Select(i => i.ToString()).ToArray();
    }

    public void ProcessPhase(SortAlgorithm.Contexts.SortPhase phase, int p1, int p2, int p3)
    {
        switch (phase)
        {
            case SortAlgorithm.Contexts.SortPhase.DistributionCount:
                _phase = DistributionPhase.Count;
                _activeBucket = -1;
                break;
            case SortAlgorithm.Contexts.SortPhase.DistributionAccumulate:
                _phase = DistributionPhase.Count; // 引き続きヒストグラム表示
                _activeBucket = -1;
                _countsReady = true; // prefix sum 完了 → ここで初めてヒストグラムを表示
                break;
            case SortAlgorithm.Contexts.SortPhase.DistributionWrite:
                _phase = DistributionPhase.Scatter;
                _activeBucket = -1;
                break;
        }
    }

    public void Process(SortOperation op, int[] mainArray, Dictionary<int, int[]> buffers)
    {
        if (_boundaries == null) return;

        // 書き込み先インデックスが属するクラスをハイライト
        if (op.Type == OperationType.IndexWrite && op.BufferId1 == 0)
            _activeBucket = FindClass(op.Index1);
        else if (op.Type == OperationType.Swap && op.BufferId1 == 0)
            _activeBucket = FindClass(op.Index1);
        // Read / Compare は直前の Write 状態を保持する
    }

    public TutorialStep Decorate(TutorialStep step)
    {
        // DistributionAccumulate フェーズに達するまで表示しない
        // (それ以前は TutorialPage の BuildInitialDistributionSnapshot が "Waiting..." を表示)
        if (_counts == null || _boundaries == null || !_countsReady)
            return step;

        var snapshot = new DistributionSnapshot
        {
            BucketCount = _totalClasses,
            BucketLabels = _labels ?? [],
            // in-place 置換のためバケットリストは使用しない
            Buckets = Enumerable.Range(0, _totalClasses).Select(_ => Array.Empty<int>()).ToArray(),
            Phase = _phase,
            ActiveBucketIndex = _activeBucket,
            ActiveElementInBucket = -1,
            Counts = (int[])_counts.Clone(),
            UseHistogram = true,
        };

        return step with { Distribution = snapshot };
    }

    public void PostStep() { }

    /// <summary>
    /// <paramref name="index"/> が属するクラスを二分探索で特定する。
    /// boundary[k] は exclusive upper bound なので boundary[k-1] ≤ index &lt; boundary[k] のとき class k。
    /// </summary>
    private int FindClass(int index)
    {
        int lo = 0, hi = _totalClasses - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if (index < _boundaries![mid])
                hi = mid;
            else
                lo = mid + 1;
        }
        return lo;
    }

    /// <summary>
    /// int 値を FlashSort と同一の符号なし順序保持キーに変換する。
    /// 符号ビットを反転することで負数が正数より小さい順序を保持する。
    /// </summary>
    private static ulong ToUnsignedKey(int value)
        => (uint)value ^ 0x8000_0000u;

    /// <summary>FlashSort と同一の線形補間クラス割り当て。</summary>
    private static int ClassOf(ulong key, ulong minKey, ulong range, int m)
        => (int)((UInt128)(m - 1) * (key - minKey) / range);
}
