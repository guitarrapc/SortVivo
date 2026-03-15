namespace SortAlgorithm.Benchmark;

[MemoryDiagnoser]
[RankColumn]
public class StringBenchmark
{
    [Params(256, 1024, 8192)]
    public int Size { get; set; }

    [Params(DataPattern.Random, DataPattern.SingleElementMoved, DataPattern.Sorted, DataPattern.Reversed, DataPattern.PipeOrgan)]
    public DataPattern Pattern { get; set; }

    private string[] _quickArray = default!;
    private string[] _quick3wayArray = default!;
    private string[] _quickMedian3Array = default!;
    private string[] _quickMedian9Array = default!;
    private string[] _quickDualPivotArray = default!;
    private string[] _stableQuickArray = default!;
    private string[] _introArray = default!;
    private string[] _introDotnetArray = default!;
    private string[] _pdqArray = default!;
    private string[] _stdArray = default!;
    private string[] _blockQuickArray = default!;
    private string[] _dotnetArray = default!;

    [IterationSetup]
    public void Setup()
    {
        _quickArray = BenchmarkData.GenerateStringArray(Size, Pattern);
        _quick3wayArray = BenchmarkData.GenerateStringArray(Size, Pattern);
        _quickMedian3Array = BenchmarkData.GenerateStringArray(Size, Pattern);
        _quickMedian9Array = BenchmarkData.GenerateStringArray(Size, Pattern);
        _quickDualPivotArray = BenchmarkData.GenerateStringArray(Size, Pattern);
        _stableQuickArray = BenchmarkData.GenerateStringArray(Size, Pattern);
        _introArray = BenchmarkData.GenerateStringArray(Size, Pattern);
        _introDotnetArray = BenchmarkData.GenerateStringArray(Size, Pattern);
        _pdqArray = BenchmarkData.GenerateStringArray(Size, Pattern);
        _stdArray = BenchmarkData.GenerateStringArray(Size, Pattern);
        _blockQuickArray = BenchmarkData.GenerateStringArray(Size, Pattern);
        _dotnetArray = BenchmarkData.GenerateStringArray(Size, Pattern);
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
