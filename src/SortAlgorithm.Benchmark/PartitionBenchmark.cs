namespace SortAlgorithm.Benchmark;

[MemoryDiagnoser]
[RankColumn]
public class PartitionBenchmark
{
    [Params(256, 1024, 8192)]
    public int Size { get; set; }

    [Params(DataPattern.Random, DataPattern.SingleElementMoved, DataPattern.Sorted, DataPattern.Reversed, DataPattern.PipeOrgan, DataPattern.AntiQuicksort)]
    public DataPattern Pattern { get; set; }

    private int[] _quickArray = default!;
    private int[] _quick3wayArray = default!;
    private int[] _quickMedian3Array = default!;
    private int[] _quickMedian9Array = default!;
    private int[] _quickDualPivotArray = default!;
    private int[] _stableQuickArray = default!;
    private int[] _introArray = default!;
    private int[] _introDotnetArray = default!;
    private int[] _pdqArray = default!;
    private int[] _stdArray = default!;
    private int[] _blockQuickArray = default!;
    private int[] _dotnetArray = default!;

    [IterationSetup]
    public void Setup()
    {
        _quickArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _quick3wayArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _quickMedian3Array = BenchmarkData.GenerateIntArray(Size, Pattern);
        _quickMedian9Array = BenchmarkData.GenerateIntArray(Size, Pattern);
        _quickDualPivotArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _stableQuickArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _introArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _introDotnetArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _pdqArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _stdArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _blockQuickArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _dotnetArray = BenchmarkData.GenerateIntArray(Size, Pattern);
    }

    [Benchmark(Baseline = true)]
    public void QuickSort()
    {
        SortAlgorithm.Algorithms.QuickSort.Sort(_quickArray.AsSpan());
    }

    [Benchmark]
    public void QuickSort3way()
    {
        SortAlgorithm.Algorithms.QuickSort3way.Sort(_quick3wayArray.AsSpan());
    }

    [Benchmark]
    public void QuickSortMedian3()
    {
        SortAlgorithm.Algorithms.QuickSortMedian3.Sort(_quickMedian3Array.AsSpan());
    }

    [Benchmark]
    public void QuickSortMedian9()
    {
        SortAlgorithm.Algorithms.QuickSortMedian9.Sort(_quickMedian9Array.AsSpan());
    }

    [Benchmark]
    public void QuickSortDualPivot()
    {
        SortAlgorithm.Algorithms.QuickSortDualPivot.Sort(_quickDualPivotArray.AsSpan());
    }

    [Benchmark]
    public void StableQuickSort()
    {
        SortAlgorithm.Algorithms.StableQuickSort.Sort(_stableQuickArray.AsSpan());
    }

    [Benchmark]
    public void IntroSort()
    {
        SortAlgorithm.Algorithms.IntroSort.Sort(_introArray.AsSpan());
    }

    [Benchmark]
    public void IntroSortDotnet()
    {
        SortAlgorithm.Algorithms.IntroSortDotnet.Sort(_introDotnetArray.AsSpan());
    }

    [Benchmark]
    public void PDQSort()
    {
        SortAlgorithm.Algorithms.PDQSort.Sort(_pdqArray.AsSpan());
    }

    [Benchmark]
    public void StdSort()
    {
        SortAlgorithm.Algorithms.StdSort.Sort(_stdArray.AsSpan());
    }

    [Benchmark]
    public void BlockQuickSort()
    {
        SortAlgorithm.Algorithms.BlockQuickSort.Sort(_blockQuickArray.AsSpan());
    }

    [Benchmark]
    public void DotnetSort()
    {
        _dotnetArray.AsSpan().Sort();
    }
}
