namespace SortAlgorithm.Benchmark;

[MemoryDiagnoser]
[RankColumn]
public class MergeBenchmark
{
    [Params(256, 1024, 8192)]
    public int Size { get; set; }

    [Params(DataPattern.Random, DataPattern.Sorted, DataPattern.Reversed, DataPattern.AntiQuicksort)]
    public DataPattern Pattern { get; set; }

    private int[] _bottomupmergeArray = default!;
    private int[] _mergeArray = default!;
    private int[] _naturalmergeArray = default!;
    private int[] _powerArray = default!;
    private int[] _rotatemergeRecursiveArray = default!;
    private int[] _rotatemergeArray = default!;
    private int[] _shiftArray = default!;
    private int[] _symmergeArray = default!;
    private int[] _timArray = default!;

    [IterationSetup]
    public void Setup()
    {
        _bottomupmergeArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _mergeArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _naturalmergeArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _powerArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _rotatemergeRecursiveArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _rotatemergeArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _shiftArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _symmergeArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _timArray = BenchmarkData.GenerateIntArray(Size, Pattern);
    }

    [Benchmark]
    public void BottomupMergeSort()
    {
        SortAlgorithm.Algorithms.BottomupMergeSort.Sort(_bottomupmergeArray.AsSpan());
    }

    [Benchmark(Baseline = true)]
    public void MergeSort()
    {
        SortAlgorithm.Algorithms.MergeSort.Sort(_mergeArray.AsSpan());
    }

    [Benchmark(Baseline = true)]
    public void NaturalMergeSort()
    {
        SortAlgorithm.Algorithms.NaturalMergeSort.Sort(_naturalmergeArray.AsSpan());
    }

    [Benchmark]
    public void PowerSort()
    {
        SortAlgorithm.Algorithms.PowerSort.Sort(_powerArray.AsSpan());
    }

    [Benchmark]
    public void RotateMergeSort()
    {
        SortAlgorithm.Algorithms.RotateMergeSort.Sort(_rotatemergeArray.AsSpan());
    }

    [Benchmark]
    public void RotateMergeSortRecursive()
    {
        SortAlgorithm.Algorithms.RotateMergeSortRecursive.Sort(_rotatemergeRecursiveArray.AsSpan());
    }

    [Benchmark]
    public void SymMergeSort()
    {
        SortAlgorithm.Algorithms.SymMergeSort.Sort(_symmergeArray.AsSpan());
    }

    [Benchmark]
    public void ShiftSort()
    {
        SortAlgorithm.Algorithms.ShiftSort.Sort(_shiftArray.AsSpan());
    }

    [Benchmark]
    public void TimSort()
    {
        SortAlgorithm.Algorithms.TimSort.Sort(_timArray.AsSpan());
    }
}
