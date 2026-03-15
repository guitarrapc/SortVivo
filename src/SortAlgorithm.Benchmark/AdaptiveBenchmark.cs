namespace SortAlgorithm.Benchmark;

[MemoryDiagnoser]
[RankColumn]
public class AdaptiveBenchmark
{
    [Params(256, 1024, 8192)]
    public int Size { get; set; }

    [Params(DataPattern.Random, DataPattern.SingleElementMoved, DataPattern.Sorted, DataPattern.Reversed, DataPattern.PipeOrgan)]
    public DataPattern Pattern { get; set; }

    private int[] _dropMergeArray = default!;
    private int[] _patienceArray = default!;

    [IterationSetup]
    public void Setup()
    {
        _dropMergeArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _patienceArray = BenchmarkData.GenerateIntArray(Size, Pattern);
    }

    [Benchmark]
    public void DropMergeSort()
    {
        SortAlgorithm.Algorithms.DropMergeSort.Sort(_dropMergeArray.AsSpan());
    }

    [Benchmark]
    public void PatienceSort()
    {
        SortAlgorithm.Algorithms.PatienceSort.Sort(_patienceArray.AsSpan());
    }
}

[MemoryDiagnoser]
[RankColumn]
public class AdaptiveSlowBenchmark
{
    [Params(256, 1024)]
    public int Size { get; set; }

    [Params(DataPattern.Random, DataPattern.SingleElementMoved, DataPattern.Sorted, DataPattern.Reversed, DataPattern.PipeOrgan)]
    public DataPattern Pattern { get; set; }

    private int[] _strandArray = default!;

    [IterationSetup]
    public void Setup()
    {
        _strandArray = BenchmarkData.GenerateIntArray(Size, Pattern);
    }

    [Benchmark]
    public void StrandSort()
    {
        SortAlgorithm.Algorithms.StrandSort.Sort(_strandArray.AsSpan());
    }
}
