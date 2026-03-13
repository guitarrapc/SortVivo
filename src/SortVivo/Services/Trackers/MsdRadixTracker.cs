using SortVivo.Models;

namespace SortVivo.Services;

/// <summary>
/// DigitBucketMsd ビジュアライゼーション用トラッカー。
/// MSD Radix sort (b=10 / b=4) の桁バケット状態を再帰レベルごとに追跡し、各ステップへ付加する。
/// </summary>
sealed class MsdRadixTracker : IVisualizationTracker
{
    private readonly int _radix;
    private readonly int _initialLength;
    private readonly int _bucketCount;
    private readonly string[] _bucketLabels;
    private readonly List<int>[] _buckets;
    private readonly int[] _shadowTemp;
    private DistributionPhase _phase;
    private int _digitIndex;
    private readonly int _maxDigit;
    private int _activeStart;
    private int _activeLength;
    private int _countPhaseReadCount;
    private bool _inCountPhase;

    // Decorate() 用キャッシュ
    private DistributionSnapshot? _cachedSnapshot;
    private string? _cachedNarrative;

    private static readonly uint[] Pow10 =
    [
        1u, 10u, 100u, 1_000u, 10_000u, 100_000u, 1_000_000u, 10_000_000u, 100_000_000u, 1_000_000_000u
    ];

    internal MsdRadixTracker(int[] initialArray, int lsdRadix)
    {
        _radix = lsdRadix;
        _initialLength = initialArray.Length;
        _bucketCount = lsdRadix > 0 ? lsdRadix : 10;
        _bucketLabels = Enumerable.Range(0, _bucketCount).Select(i => i.ToString()).ToArray();
        _buckets = Enumerable.Range(0, _bucketCount).Select(_ => new List<int>()).ToArray();
        _shadowTemp = new int[initialArray.Length];
        _phase = DistributionPhase.Scatter;
        _activeStart = 0;
        _activeLength = initialArray.Length;

        // 最上位桁から開始: 初期配列の最大値から桁数を計算
        if (initialArray.Length > 0)
        {
            var maxVal = initialArray.Max();
            if (lsdRadix == 4)
            {
                uint key = (uint)maxVal ^ 0x8000_0000u;
                _maxDigit = key > 0 ? (32 - System.Numerics.BitOperations.LeadingZeroCount(key) + 1) / 2 : 1;
            }
            else // b=10
            {
                ulong absMax = (ulong)Math.Abs(maxVal);
                _maxDigit = absMax > 0 ? (int)Math.Floor(Math.Log10(absMax)) + 1 : 1;
            }
        }
        else
        {
            _maxDigit = 1;
        }
        _digitIndex = _maxDigit - 1;
    }

    public void Process(SortOperation op, int[] mainArray, Dictionary<int, int[]> buffers)
    {
        int distActiveBucket = -1;
        int distActiveElement = -1;

        if (op.Type == OperationType.IndexRead && op.BufferId1 == 0)
        {
            // Count フェーズ: 現在範囲内の連続 Read
            if (!_inCountPhase && (_countPhaseReadCount == 0 || (op.Index1 >= _activeStart && op.Index1 < _activeStart + _activeLength)))
            {
                _inCountPhase = true;
                _countPhaseReadCount = 0;
            }

            if (_inCountPhase)
            {
                int v = mainArray[op.Index1];
                int digit = ComputeMsdDigit(v, _digitIndex, _radix);
                if ((uint)digit < (uint)_bucketCount)
                {
                    distActiveBucket = digit;
                    _countPhaseReadCount++;
                    _phase = DistributionPhase.Scatter;
                }
            }
        }
        else if (op.Type == OperationType.IndexWrite && op.BufferId1 == 1 && op.Value.HasValue)
        {
            // Distribute フェーズ: temp への Write
            _inCountPhase = false;
            int v = op.Value.Value;
            int digit = ComputeMsdDigit(v, _digitIndex, _radix);
            if ((uint)digit < (uint)_bucketCount)
            {
                _buckets[digit].Add(v);
                if (op.Index1 < _shadowTemp.Length)
                    _shadowTemp[op.Index1] = v;
                distActiveBucket = digit;
                distActiveElement = _buckets[digit].Count - 1;
            }
            _phase = DistributionPhase.Scatter;
        }
        else if (op.Type == OperationType.RangeCopy && op.BufferId1 == 1 && op.BufferId2 == 0)
        {
            // CopyTo: 現在の再帰レベル終了、次レベルへ
            distActiveBucket = -1;
            _phase = DistributionPhase.Gather;
            _countPhaseReadCount = 0;
            foreach (var b in _buckets) b.Clear();
            if (_digitIndex > 0)
                _digitIndex--;
        }

        _cachedSnapshot = new DistributionSnapshot
        {
            BucketCount = _bucketCount,
            BucketLabels = _bucketLabels,
            Buckets = _buckets.Select(b => b.ToArray()).ToArray(),
            Phase = _phase,
            ActiveBucketIndex = distActiveBucket,
            ActiveElementInBucket = distActiveElement,
            PassIndex = _maxDigit - _digitIndex - 1,
            PassLabel = GetMsdPassLabel(_digitIndex, _radix),
            ActiveRange = (_activeStart, _activeLength),
            DigitIndex = _digitIndex,
        };

        // ナラティブ上書き
        string passLabel = GetMsdPassLabel(_digitIndex, _radix);
        string rangeLabel = _activeLength < _initialLength
            ? $" (range [{_activeStart}..{_activeStart + _activeLength - 1}])"
            : "";
        int mainReadValue = op.Index1 >= 0 && op.Index1 < mainArray.Length ? mainArray[op.Index1] : 0;

        _cachedNarrative = (op.Type, op.Value.HasValue) switch
        {
            (OperationType.IndexRead, _) when _inCountPhase
                => $"Count value {mainReadValue} for {passLabel}{rangeLabel}",
            (OperationType.IndexWrite, true) when distActiveBucket >= 0
                => $"Distribute value {op.Value!.Value} into bucket [{_bucketLabels[distActiveBucket]}] ({passLabel}{rangeLabel})",
            (OperationType.RangeCopy, _)
                => "Copy sorted range back to main — recurse into sub-buckets",
            _ => null,
        };
    }

    public TutorialStep Decorate(TutorialStep step)
    {
        if (_cachedSnapshot == null) return step;
        return step with
        {
            Distribution = _cachedSnapshot,
            Narrative = _cachedNarrative ?? step.Narrative,
        };
    }

    public void PostStep() { }

    // MSD helpers

    /// <summary>
    /// MSD ソート用の桁インデックスを計算する（符号ビット反転キーベース）。
    /// radix=10: (key / 10^digitIndex) % 10
    /// radix=4:  (key >> (digitIndex * 2)) &amp; 0b11
    /// </summary>
    private static int ComputeMsdDigit(int v, int digitIndex, int radix)
    {
        if (radix == 4)
        {
            uint key = (uint)v ^ 0x8000_0000u;
            int shift = digitIndex * 2;
            return (int)((key >> shift) & 0b11u);
        }
        else // radix == 10 (default)
        {
            ulong key = (ulong)v ^ 0x8000_0000_0000_0000UL;
            ulong divisor = (uint)digitIndex < (uint)Pow10.Length ? Pow10[digitIndex] : 1UL;
            return (int)((key / divisor) % 10UL);
        }
    }

    /// <summary>
    /// MSD 桁インデックスと基数からパスラベル文字列を生成する。
    /// digitIndex は最上位桁から降順（digitIndex=1 → tens, digitIndex=0 → ones）。
    /// </summary>
    private static string GetMsdPassLabel(int digitIndex, int radix)
    {
        if (radix == 4)
        {
            int startBit = digitIndex * 2;
            int endBit = startBit + 1;
            return $"bits {startBit}-{endBit}";
        }
        return digitIndex switch
        {
            0 => "ones digit",
            1 => "tens digit",
            2 => "hundreds digit",
            3 => "thousands digit",
            _ => $"10^{digitIndex} digit"
        };
    }
}
