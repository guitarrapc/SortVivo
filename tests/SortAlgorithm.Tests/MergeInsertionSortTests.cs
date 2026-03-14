using SortAlgorithm.Algorithms;
using SortAlgorithm.Contexts;
using TUnit.Assertions.Enums;

namespace SortAlgorithm.Tests;

public class MergeInsertionSortTests
{
    [Test]
    [MethodDataSource(typeof(MockRandomData), nameof(MockRandomData.Generate))]
    [MethodDataSource(typeof(MockNegativePositiveRandomData), nameof(MockNegativePositiveRandomData.Generate))]
    [MethodDataSource(typeof(MockNegativeRandomData), nameof(MockNegativeRandomData.Generate))]
    [MethodDataSource(typeof(MockReversedData), nameof(MockReversedData.Generate))]
    [MethodDataSource(typeof(MockPipeorganData), nameof(MockPipeorganData.Generate))]
    [MethodDataSource(typeof(MockNearlySortedData), nameof(MockNearlySortedData.Generate))]
    [MethodDataSource(typeof(MockSameValuesData), nameof(MockSameValuesData.Generate))]
    [MethodDataSource(typeof(MockQuickSortWorstCaseData), nameof(MockQuickSortWorstCaseData.Generate))]
    [MethodDataSource(typeof(MockTwoDistinctValuesData), nameof(MockTwoDistinctValuesData.Generate))]
    [MethodDataSource(typeof(MockHalfZeroHalfOneData), nameof(MockHalfZeroHalfOneData.Generate))]
    [MethodDataSource(typeof(MockValleyRandomData), nameof(MockValleyRandomData.Generate))]
    [MethodDataSource(typeof(MockHighlySkewedData), nameof(MockHighlySkewedData.Generate))]
    public async Task SortResultOrderTest(IInputSample<int> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 512, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        MergeInsertionSort.Sort(array.AsSpan(), stats);

        // Check is sorted
        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockNanRandomData), nameof(MockNanRandomData.GenerateHalf))]
    public async Task SortHalfResultOrderTest(IInputSample<Half> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 512, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        MergeInsertionSort.Sort(array.AsSpan(), stats);

        // Check is sorted
        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockNanRandomData), nameof(MockNanRandomData.GenerateFloat))]
    public async Task SortFloatResultOrderTest(IInputSample<float> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 512, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        MergeInsertionSort.Sort(array.AsSpan(), stats);

        // Check is sorted
        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockNanRandomData), nameof(MockNanRandomData.GenerateDouble))]
    public async Task SortDoubleResultOrderTest(IInputSample<double> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 512, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        MergeInsertionSort.Sort(array.AsSpan(), stats);

        // Check is sorted
        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockRandomData), nameof(MockRandomData.Generate))]
    public async Task SortNoStatistics(IInputSample<int> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 512, "Skip large inputs for no stats test");

        var array = inputSample.Samples.ToArray();

        MergeInsertionSort.Sort(array.AsSpan());

        // Check is sorted
        for (var i = 0; i < array.Length - 1; i++)
        {
            await Assert.That(array[i]).IsLessThanOrEqualTo(array[i + 1]);
        }
    }

    [Test]
    public async Task SortEmptyArray()
    {
        var stats = new StatisticsContext();
        var array = Array.Empty<int>();

        MergeInsertionSort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(Array.Empty<int>(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task SortSingleElementArray()
    {
        var stats = new StatisticsContext();
        var array = new[] { 42 };

        MergeInsertionSort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(new[] { 42 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task SortTwoElementArray()
    {
        var stats = new StatisticsContext();
        var array = new[] { 2, 1 };

        MergeInsertionSort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(new[] { 1, 2 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task SortAlreadySortedArray()
    {
        var stats = new StatisticsContext();
        var array = new[] { 1, 2, 3, 4, 5 };

        MergeInsertionSort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task SortReverseSortedArray()
    {
        var stats = new StatisticsContext();
        var array = new[] { 5, 4, 3, 2, 1 };

        MergeInsertionSort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 }, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockSortedData), nameof(MockSortedData.Generate))]
    public async Task StatisticsSortedTest(IInputSample<int> inputSample)
    {
        if (inputSample.Samples.Length > 1024)
            return;

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();
        MergeInsertionSort.Sort(array.AsSpan(), stats);

        // MergeInsertionSort reads all n elements upfront and writes all n elements at the end
        await Assert.That((ulong)array.Length).IsEqualTo((ulong)inputSample.Samples.Length);
        await Assert.That(stats.IndexReadCount).IsGreaterThan(0UL);
        await Assert.That(stats.IndexWriteCount).IsGreaterThan(0UL);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL); // MergeInsertionSort doesn't use swaps
    }

    [Test]
    [Arguments(10)]
    [Arguments(20)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task TheoreticalValuesSortedTest(int n)
    {
        var stats = new StatisticsContext();
        var sorted = Enumerable.Range(0, n).ToArray();
        MergeInsertionSort.Sort(sorted.AsSpan(), stats);

        // Ford-Johnson comparison count is near-optimal for all inputs:
        // approximately n⌈log₂ n⌉ - 2^⌈log₂ n⌉ + 1, close to ⌈log₂(n!)⌉
        ulong minCompares = (ulong)(n - 1);
        ulong maxCompares = (ulong)(3 * n * Math.Max(1, Math.Log(n, 2)));

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);

        // MergeInsertionSort always reads exactly n elements and writes exactly n elements
        await Assert.That(stats.IndexReadCount).IsEqualTo((ulong)n);
        await Assert.That(stats.IndexWriteCount).IsEqualTo((ulong)n);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL);
    }

    [Test]
    [Arguments(10)]
    [Arguments(20)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task TheoreticalValuesReversedTest(int n)
    {
        var stats = new StatisticsContext();
        var reversed = Enumerable.Range(0, n).Reverse().ToArray();
        MergeInsertionSort.Sort(reversed.AsSpan(), stats);

        // Ford-Johnson comparison count is input-independent (same near-optimal count for reversed data)
        // Writes are O(n²) in worst case due to binary insertion shifts, but read/write count via span is exactly n
        ulong minCompares = (ulong)(n - 1);
        ulong maxCompares = (ulong)(n * n);

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);

        await Assert.That(stats.IndexReadCount).IsEqualTo((ulong)n);
        await Assert.That(stats.IndexWriteCount).IsEqualTo((ulong)n);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL);
    }

    [Test]
    [Arguments(10)]
    [Arguments(20)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task TheoreticalValuesRandomTest(int n)
    {
        var stats = new StatisticsContext();
        var random = Enumerable.Range(0, n).OrderBy(_ => Guid.NewGuid()).ToArray();
        MergeInsertionSort.Sort(random.AsSpan(), stats);

        // Ford-Johnson maintains near-optimal comparison count regardless of input order
        ulong minCompares = (ulong)(n - 1);
        ulong maxCompares = (ulong)(n * n);

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);

        await Assert.That(stats.IndexReadCount).IsEqualTo((ulong)n);
        await Assert.That(stats.IndexWriteCount).IsEqualTo((ulong)n);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL);
    }

    [Test]
    [Arguments(5)]
    [Arguments(10)]
    [Arguments(20)]
    [Arguments(50)]
    public async Task TheoreticalValuesSameElementsTest(int n)
    {
        var stats = new StatisticsContext();
        var sameValues = Enumerable.Repeat(42, n).ToArray();
        MergeInsertionSort.Sort(sameValues.AsSpan(), stats);

        // Ford-Johnson compares equal elements identically to distinct elements
        ulong minCompares = (ulong)(n - 1);
        ulong maxCompares = (ulong)(n * Math.Max(1, (int)Math.Log(n, 2)) * 3);

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);

        // Verify all values remain correct
        foreach (var item in sameValues) await Assert.That(item).IsEqualTo(42);

        await Assert.That(stats.IndexReadCount).IsEqualTo((ulong)n);
        await Assert.That(stats.IndexWriteCount).IsEqualTo((ulong)n);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL);
    }
}
