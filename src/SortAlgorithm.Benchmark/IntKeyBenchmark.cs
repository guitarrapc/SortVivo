using SortAlgorithm.Utils;

namespace SortAlgorithm.Benchmark;

[MemoryDiagnoser]
[RankColumn]
public class IntKeyBenchmark
{
    [Params(256, 1024, 8192)]
    public int Size { get; set; }

    [Params(DataPattern.Random, DataPattern.SingleElementMoved, DataPattern.Sorted, DataPattern.Reversed, DataPattern.PipeOrgan)]
    public DataPattern Pattern { get; set; }

    private IntKey[] _quickArray = default!;
    private IntKey[] _quick3wayArray = default!;
    private IntKey[] _quickMedian3Array = default!;
    private IntKey[] _quickMedian9Array = default!;
    private IntKey[] _quickDualPivotArray = default!;
    private IntKey[] _stableQuickArray = default!;
    private IntKey[] _introArray = default!;
    private IntKey[] _introDotnetArray = default!;
    private IntKey[] _pdqArray = default!;
    private IntKey[] _stdArray = default!;
    private IntKey[] _blockQuickArray = default!;
    private IntKey[] _dotnetArray = default!;

    [IterationSetup]
    public void Setup()
    {
        _quickArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _quick3wayArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _quickMedian3Array = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _quickMedian9Array = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _quickDualPivotArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _stableQuickArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _introArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _introDotnetArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _pdqArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _stdArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _blockQuickArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
        _dotnetArray = BenchmarkData.GenerateIntKeyArray(Size, Pattern);
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
