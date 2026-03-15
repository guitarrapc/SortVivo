namespace SortAlgorithm.Benchmark;

[MemoryDiagnoser]
[RankColumn]
public class SelectionBenchmark
{
    [Params(256, 1024)]
    public int Size { get; set; }

    [Params(DataPattern.Random, DataPattern.SingleElementMoved, DataPattern.Sorted, DataPattern.Reversed, DataPattern.PipeOrgan)]
    public DataPattern Pattern { get; set; }

    private int[] _selectionArray = default!;
    private int[] _doubleSelectionArray = default!;
    private int[] _cycleArray = default!;
    private int[] _pancakeArray = default!;

    [IterationSetup]
    public void Setup()
    {
        _selectionArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _doubleSelectionArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _cycleArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _pancakeArray = BenchmarkData.GenerateIntArray(Size, Pattern);
    }

    [Benchmark(Baseline = true)]
    public void SelectionSort()
    {
        SortAlgorithm.Algorithms.SelectionSort.Sort(_selectionArray.AsSpan());
    }

    [Benchmark]
    public void DoubleSelectionSort()
    {
        SortAlgorithm.Algorithms.DoubleSelectionSort.Sort(_cycleArray.AsSpan());
    }

    [Benchmark]
    public void CycleSort()
    {
        SortAlgorithm.Algorithms.CycleSort.Sort(_cycleArray.AsSpan());
    }

    [Benchmark]
    public void PancakeSort()
    {
        SortAlgorithm.Algorithms.PancakeSort.Sort(_pancakeArray.AsSpan());
    }
}
