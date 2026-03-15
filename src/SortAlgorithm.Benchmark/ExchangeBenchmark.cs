namespace SortAlgorithm.Benchmark;

[MemoryDiagnoser]
[RankColumn]
public class ExchangeBenchmark
{
    [Params(256, 1024)]
    public int Size { get; set; }

    [Params(DataPattern.Random, DataPattern.SingleElementMoved, DataPattern.Sorted, DataPattern.Reversed, DataPattern.PipeOrgan, DataPattern.AntiQuicksort)]
    public DataPattern Pattern { get; set; }

    private int[] _bubbleArray = default!;
    private int[] _cocktailShakerArray = default!;
    private int[] _oddEvenArray = default!;
    private int[] _combArray = default!;

    [IterationSetup]
    public void Setup()
    {
        _bubbleArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _cocktailShakerArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _oddEvenArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _combArray = BenchmarkData.GenerateIntArray(Size, Pattern);
    }

    [Benchmark(Baseline = true)]
    public void BubbleSort()
    {
        SortAlgorithm.Algorithms.BubbleSort.Sort(_bubbleArray.AsSpan());
    }

    [Benchmark]
    public void CocktailShakerSort()
    {
        SortAlgorithm.Algorithms.CocktailShakerSort.Sort(_cocktailShakerArray.AsSpan());
    }

    [Benchmark]
    public void OddEvenSort()
    {
        SortAlgorithm.Algorithms.OddEvenSort.Sort(_oddEvenArray.AsSpan());
    }

    [Benchmark]
    public void CombSort()
    {
        SortAlgorithm.Algorithms.CombSort.Sort(_combArray.AsSpan());
    }
}
