namespace SortAlgorithm.Benchmark;

[MemoryDiagnoser]
[RankColumn]
public class InsertionBenchmark
{
    [Params(256, 1024)]
    public int Size { get; set; }

    [Params(DataPattern.Random, DataPattern.SingleElementMoved, DataPattern.Sorted, DataPattern.Reversed, DataPattern.PipeOrgan)]
    public DataPattern Pattern { get; set; }

    private int[] _insertionArray = default!;
    private int[] _pairinsertiontreeArray = default!;
    private int[] _binaryinsertArray = default!;
    private int[] _gnomeArray = default!;
    private int[] _libraryArray = default!;
    private int[] _mergeinsertionArray = default!;
    private int[] _shellArrayCiura2001 = default!;
    private int[] _shellArrayKnuth1973 = default!;
    private int[] _shellArrayLee2021 = default!;
    private int[] _shellArraySedgewick1986 = default!;
    private int[] _shellArrayTokuda1992 = default!;

    [IterationSetup]
    public void Setup()
    {
        _insertionArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _pairinsertiontreeArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _binaryinsertArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _gnomeArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _libraryArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _mergeinsertionArray = BenchmarkData.GenerateIntArray(Size, Pattern);
        _shellArrayCiura2001 = BenchmarkData.GenerateIntArray(Size, Pattern);
        _shellArrayKnuth1973 = BenchmarkData.GenerateIntArray(Size, Pattern);
        _shellArrayLee2021 = BenchmarkData.GenerateIntArray(Size, Pattern);
        _shellArraySedgewick1986 = BenchmarkData.GenerateIntArray(Size, Pattern);
        _shellArrayTokuda1992 = BenchmarkData.GenerateIntArray(Size, Pattern);
    }

    [Benchmark(Baseline = true)]
    public void InsertionSort()
    {
        SortAlgorithm.Algorithms.InsertionSort.Sort(_insertionArray.AsSpan());
    }

    [Benchmark]
    public void PairInsertionSort()
    {
        SortAlgorithm.Algorithms.PairInsertionSort.Sort(_pairinsertiontreeArray.AsSpan());
    }

    [Benchmark]
    public void BinaryInsertSort()
    {
        SortAlgorithm.Algorithms.BinaryInsertionSort.Sort(_binaryinsertArray.AsSpan());
    }

    [Benchmark]
    public void GnomeSort()
    {
        SortAlgorithm.Algorithms.GnomeSort.Sort(_gnomeArray.AsSpan());
    }

    [Benchmark]
    public void LibrarySort()
    {
        SortAlgorithm.Algorithms.LibrarySort.Sort(_insertionArray.AsSpan());
    }

    [Benchmark]
    public void MergeInsertionSort()
    {
        SortAlgorithm.Algorithms.MergeInsertionSort.Sort(_mergeinsertionArray.AsSpan());
    }

    [Benchmark]
    public void ShellSortCiura2001()
    {
        SortAlgorithm.Algorithms.ShellSortCiura2001.Sort(_shellArrayCiura2001.AsSpan());
    }

    [Benchmark]
    public void ShellSortKnuth1973()
    {
        SortAlgorithm.Algorithms.ShellSortKnuth1973.Sort(_shellArrayKnuth1973.AsSpan());
    }

    [Benchmark]
    public void ShellSortLee2021()
    {
        SortAlgorithm.Algorithms.ShellSortLee2021.Sort(_shellArrayLee2021.AsSpan());
    }

    [Benchmark]
    public void ShellSortSedgewick1986()
    {
        SortAlgorithm.Algorithms.ShellSortSedgewick1986.Sort(_shellArraySedgewick1986.AsSpan());
    }

    [Benchmark]
    public void ShellSortTokuda1992()
    {
        SortAlgorithm.Algorithms.ShellSortTokuda1992.Sort(_shellArrayTokuda1992.AsSpan());
    }
}
