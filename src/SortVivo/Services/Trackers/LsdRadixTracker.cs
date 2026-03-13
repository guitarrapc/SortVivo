using SortVivo.Models;

namespace SortVivo.Services;

/// <summary>
/// DigitBucketLsd ビジュアライゼーション用トラッカー。
/// LSD Radix sort (b=10 / b=4) の桁バケット状態をパスごとに追跡し、各ステップへ付加する。
/// </summary>
sealed class LsdRadixTracker : IVisualizationTracker
{
    private readonly int _radix;
    private readonly int _minValue;
    private readonly int _bucketCount;
    private readonly string[] _bucketLabels;
    private readonly List<int>[] _buckets;
    private readonly int[] _shadowTemp;
    private DistributionPhase _phase;
    private int _passIndex;
    private int _prevReadSourceId;
    private bool _phaseReady;
    private bool _clearBucketsAfterStep;

    // Decorate() 用キャッシュ
    private DistributionSnapshot? _cachedSnapshot;
    private string? _cachedNarrative;

    private static readonly uint[] Pow10 =
    [
        1u, 10u, 100u, 1_000u, 10_000u, 100_000u, 1_000_000u, 10_000_000u, 100_000_000u, 1_000_000_000u
    ];

    internal LsdRadixTracker(int[] initialArray, int lsdRadix)
    {
        _radix = lsdRadix;
        _minValue = initialArray.Length > 0 ? initialArray.Min() : 0;
        _bucketCount = lsdRadix > 0 ? lsdRadix : 10;
        _bucketLabels = Enumerable.Range(0, _bucketCount).Select(i => i.ToString()).ToArray();
        _buckets = Enumerable.Range(0, _bucketCount).Select(_ => new List<int>()).ToArray();
        _shadowTemp = new int[initialArray.Length];
        _phase = DistributionPhase.Scatter;
        _prevReadSourceId = -1;
    }

    public void Process(SortOperation op, int[] mainArray, Dictionary<int, int[]> buffers)
    {
        int distActiveBucket = -1;
        int distActiveElement = -1;

        if (op.Type == OperationType.IndexRead)
        {
            // b=4 パス境界検出: Read のソースバッファが変わったらパス終了
            if (_phaseReady && _prevReadSourceId >= 0 && op.BufferId1 != _prevReadSourceId)
            {
                foreach (var b in _buckets) b.Clear();
                _passIndex++;
            }
            _prevReadSourceId = op.BufferId1;
            distActiveBucket = -1;
            distActiveElement = -1;
        }
        else if (op.Type == OperationType.IndexWrite && op.Value.HasValue)
        {
            _phaseReady = true;
            int v = op.Value.Value;
            int digit = ComputeLsdDigit(v, _passIndex, _radix, _minValue);
            if ((uint)digit < (uint)_bucketCount)
            {
                _buckets[digit].Add(v);
                if (op.BufferId1 == 1 && op.Index1 < _shadowTemp.Length)
                    _shadowTemp[op.Index1] = v;
                distActiveBucket = digit;
                distActiveElement = _buckets[digit].Count - 1;
            }
            _phase = DistributionPhase.Scatter;
        }
        else if (op.Type == OperationType.RangeCopy && op.BufferId1 == 1 && op.BufferId2 == 0)
        {
            // b=10 パス終了: RangeCopy(temp→main) で全バケットを一括回収
            distActiveBucket = -1;
            _phase = DistributionPhase.Gather;
            _clearBucketsAfterStep = true;
        }

        _cachedSnapshot = new DistributionSnapshot
        {
            BucketCount = _bucketCount,
            BucketLabels = _bucketLabels,
            Buckets = _buckets.Select(b => b.ToArray()).ToArray(),
            Phase = _phase,
            ActiveBucketIndex = distActiveBucket,
            ActiveElementInBucket = distActiveElement,
            PassIndex = _passIndex,
            PassLabel = GetLsdPassLabel(_passIndex, _radix),
        };

        // ナラティブ上書き
        string passLabel = GetLsdPassLabel(_passIndex, _radix);
        int[] srcArr = op.BufferId1 == 0 ? mainArray : buffers.GetValueOrDefault(op.BufferId1, mainArray);
        int readValue = op.Index1 >= 0 && op.Index1 < srcArr.Length ? srcArr[op.Index1] : 0;

        _cachedNarrative = (op.Type, op.Value.HasValue) switch
        {
            (OperationType.IndexRead, _) when !_phaseReady
                => $"Pre-compute key for value {readValue} at index {op.Index1}",
            (OperationType.IndexRead, _)
                => $"Read value {readValue} from index {op.Index1} ({passLabel})",
            (OperationType.IndexWrite, true) when distActiveBucket >= 0
                => $"Scatter value {op.Value!.Value} into digit bucket [{_bucketLabels[distActiveBucket]}] ({passLabel})",
            (OperationType.RangeCopy, _)
                => $"Gather all buckets back to main array — pass {_passIndex + 1} complete",
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

    public void PostStep()
    {
        if (_clearBucketsAfterStep)
        {
            foreach (var b in _buckets) b.Clear();
            _passIndex++;
            _clearBucketsAfterStep = false;
        }
    }

    // LSD helpers

    /// <summary>
    /// LSD ソート用の桁インデックスを計算する。
    /// radix=10: (v - minValue) / 10^passIndex % 10
    /// radix=4:  ((uint)v ^ 0x8000_0000) >> (passIndex * 2) &amp; 0b11
    /// </summary>
    private static int ComputeLsdDigit(int v, int passIndex, int radix, int minValue)
    {
        if (radix == 4)
        {
            uint key = (uint)v ^ 0x8000_0000u;
            int shift = passIndex * 2;
            return (int)((key >> shift) & 0b11u);
        }
        else // radix == 10 (default)
        {
            uint normalized = (uint)v - (uint)minValue;
            uint divisor = (uint)passIndex < (uint)Pow10.Length ? Pow10[passIndex] : 1u;
            return (int)((normalized / divisor) % 10u);
        }
    }

    /// <summary>
    /// パスインデックスと基数からパスラベル文字列を生成する。
    /// radix=10: "ones digit" / "tens digit" / ...
    /// radix=4:  "bits 0-1" / "bits 2-3" / ...
    /// </summary>
    private static string GetLsdPassLabel(int passIndex, int radix)
    {
        if (radix == 4)
        {
            int startBit = passIndex * 2;
            int endBit = startBit + 1;
            return $"bits {startBit}-{endBit}";
        }
        return passIndex switch
        {
            0 => "ones digit",
            1 => "tens digit",
            2 => "hundreds digit",
            3 => "thousands digit",
            _ => $"10^{passIndex} digit"
        };
    }
}
