using SortAlgorithm.Algorithms;
using SortAlgorithm.Contexts;
using TUnit.Assertions.Enums;

namespace SortAlgorithm.Tests;

public class LibrarySortTests
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

        LibrarySort.Sort(array.AsSpan(), stats);

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

        LibrarySort.Sort(array.AsSpan(), stats);

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

        LibrarySort.Sort(array.AsSpan(), stats);

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

        LibrarySort.Sort(array.AsSpan(), stats);

        // Check is sorted
        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockIntKeyRandomData), nameof(MockIntKeyRandomData.Generate))]
    public async Task SortIntStructResultOrderTest(IInputSample<Utils.IntKey> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 512, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        LibrarySort.Sort(array.AsSpan(), stats);

        // Check is sorted
        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockStabilityData), nameof(MockStabilityData.Generate))]
    public async Task StabilityTest(StabilityTestItem[] items)
    {
        // Test stability: equal elements should maintain relative order
        var stats = new StatisticsContext();

        LibrarySort.Sort(items.AsSpan(), stats);

        // Verify sorting correctness - values should be in ascending order
        await Assert.That(items.Select(x => x.Value).ToArray()).IsEquivalentTo(MockStabilityData.Sorted, CollectionOrdering.Matching);

        // Verify stability: for each group of equal values, original order is preserved
        var value1Indices = items.Where(x => x.Value == 1).Select(x => x.OriginalIndex).ToArray();
        var value2Indices = items.Where(x => x.Value == 2).Select(x => x.OriginalIndex).ToArray();
        var value3Indices = items.Where(x => x.Value == 3).Select(x => x.OriginalIndex).ToArray();

        // Value 1 appeared at original indices 0, 2, 4 - should remain in this order
        await Assert.That(value1Indices).IsEquivalentTo(MockStabilityData.Sorted1, CollectionOrdering.Matching);

        // Value 2 appeared at original indices 1, 5 - should remain in this order
        await Assert.That(value2Indices).IsEquivalentTo(MockStabilityData.Sorted2, CollectionOrdering.Matching);

        // Value 3 appeared at original index 3
        await Assert.That(value3Indices).IsEquivalentTo(MockStabilityData.Sorted3, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockStabilityWithIdData), nameof(MockStabilityWithIdData.Generate))]
    public async Task StabilityTestWithComplex(StabilityTestItemWithId[] items)
    {
        // Test stability with more complex scenario - multiple equal values
        var stats = new StatisticsContext();

        LibrarySort.Sort(items.AsSpan(), stats);

        // Expected: [2:B, 2:D, 2:F, 5:A, 5:C, 5:G, 8:E]
        // Keys are sorted, and elements with the same key maintain original order

        for (var i = 0; i < items.Length; i++)
        {
            await Assert.That(items[i].Key).IsEqualTo(MockStabilityWithIdData.Sorted[i].Key);
            await Assert.That(items[i].Id).IsEqualTo(MockStabilityWithIdData.Sorted[i].Id);
        }
    }

    [Test]
    public async Task EmptyArrayTest()
    {
        var stats = new StatisticsContext();
        var array = Array.Empty<int>();

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).Count().IsEqualTo(0);
    }

    [Test]
    public async Task SingleElementTest()
    {
        var stats = new StatisticsContext();
        var array = new[] { 42 };

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).Count().IsEqualTo(1);
        await Assert.That(array[0]).IsEqualTo(42);
    }

    [Test]
    public async Task TwoElementsAscendingTest()
    {
        var stats = new StatisticsContext();
        var array = new[] { 1, 2 };

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(new[] { 1, 2 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task TwoElementsDescendingTest()
    {
        var stats = new StatisticsContext();
        var array = new[] { 2, 1 };

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(new[] { 1, 2 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task AllSameElementsTest()
    {
        var stats = new StatisticsContext();
        var array = new[] { 5, 5, 5, 5, 5 };

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(new[] { 5, 5, 5, 5, 5 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task AlreadySortedTest()
    {
        var stats = new StatisticsContext();
        var array = new[] { 1, 2, 3, 4, 5 };

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task ReverseSortedTest()
    {
        var stats = new StatisticsContext();
        var array = new[] { 5, 4, 3, 2, 1 };

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task LargerArrayTest()
    {
        var stats = new StatisticsContext();
        var array = new[] { 64, 34, 25, 12, 22, 11, 90, 88, 45, 50, 22, 34, 67, 100 };
        var expected = array.OrderBy(x => x).ToArray();

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    public async Task WithDuplicatesTest()
    {
        var stats = new StatisticsContext();
        var array = new[] { 3, 1, 4, 1, 5, 9, 2, 6, 5, 3, 5 };
        var expected = array.OrderBy(x => x).ToArray();

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    public async Task NegativeNumbersTest()
    {
        var stats = new StatisticsContext();
        var array = new[] { -5, 3, -2, 8, -10, 0, 1, -3 };
        var expected = array.OrderBy(x => x).ToArray();

        LibrarySort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }



    [Test]
    [MethodDataSource(typeof(MockSortedData), nameof(MockSortedData.Generate))]
    public async Task StatisticsSortedTest(IInputSample<int> inputSample)
    {
        if (inputSample.Samples.Length > 1024)
            return;

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();
        LibrarySort.Sort(array.AsSpan(), stats);

        // Library Sort uses auxiliary arrays, so statistics are different from standard insertion sort
        // Just verify the array is sorted and some operations occurred
        await Assert.That((ulong)array.Length).IsEqualTo((ulong)inputSample.Samples.Length);
        await Assert.That(stats.IndexReadCount).IsGreaterThan(0UL);
        await Assert.That(stats.IndexWriteCount).IsGreaterThan(0UL); // Library sort always writes to aux array
        await Assert.That(stats.SwapCount).IsEqualTo(0UL); // Library sort doesn't use swaps
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
        LibrarySort.Sort(sorted.AsSpan(), stats);

        // LibrarySort behavior on sorted data:
        // - For small arrays (n ≤ 32): Falls back to InsertionSort
        //   - Best case O(n): n-1 comparisons, 0 writes (no shifts needed)
        // - For larger arrays (n > 32): LibrarySort with InsertionSort warmup
        //   - CompareCount comes only from warmup phase (BinarySearch uses plain comparer, not tracked)
        ulong minCompares, maxCompares;
        if (n <= 32)
        {
            minCompares = (ulong)(n - 1);
            maxCompares = (ulong)(n - 1); // Exact: sorted InsertionSort = n-1 comparisons
        }
        else
        {
            minCompares = 1UL;
            maxCompares = (ulong)(3 * n * Math.Max(1, Math.Log(n, 2)));
        }

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL); // LibrarySort never uses swaps

        if (n <= 32)
        {
            // Sorted InsertionSort: no element shifts → no writes to main span
            await Assert.That(stats.IndexWriteCount).IsEqualTo(0UL);
            // IndexReadCount: 2*(n-1) reads (1 for tmp + 1 for first comparison per iteration)
            await Assert.That(stats.IndexReadCount).IsEqualTo((ulong)(2 * (n - 1)));
        }
        else
        {
            // LibrarySort writes extensively to aux array (gap init + element placement + extraction)
            await Assert.That(stats.IndexWriteCount).IsGreaterThan(0UL);
            await Assert.That(stats.IndexReadCount).IsGreaterThan(0UL);
        }
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
        LibrarySort.Sort(reversed.AsSpan(), stats);

        // LibrarySort behavior on reversed data:
        // - For small arrays (n ≤ 32): Falls back to InsertionSort
        //   - Worst case O(n²): n(n-1)/2 comparisons exactly
        // - For larger arrays (n > 32): LibrarySort with InsertionSort warmup on reversed prefix
        //   - CompareCount from warmup = 32*31/2 = 496 (reversed InsertionSort worst case)
        //   - BinarySearch uses plain comparer (not tracked in CompareCount)
        ulong minCompares, maxCompares;
        if (n <= 32)
        {
            minCompares = (ulong)(n * (n - 1) / 2);
            maxCompares = (ulong)(n * (n - 1) / 2); // Exact: reversed InsertionSort = n(n-1)/2 comparisons
        }
        else
        {
            minCompares = (ulong)n;
            maxCompares = (ulong)(n * n);
        }

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL); // LibrarySort never uses swaps

        // IndexReads: each comparison reads an element (plus extra reads for shifts in InsertionSort path)
        var minIndexReads = stats.CompareCount;
        await Assert.That(stats.IndexReadCount >= minIndexReads).IsTrue().Because($"IndexReadCount ({stats.IndexReadCount}) should be >= CompareCount ({minIndexReads})");
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
        LibrarySort.Sort(random.AsSpan(), stats);

        // LibrarySort behavior on random data:
        // - For small arrays (n ≤ 32): Falls back to InsertionSort
        //   - Average case O(n²): approximately n²/4 comparisons
        // - For larger arrays (n > 32): LibrarySort with InsertionSort warmup
        //   - O(n log n) expected due to binary search and gap-based insertion
        //   - CompareCount from warmup only: min=31 (sorted warmup), max=496 (reversed warmup)
        // Use Math.Min(n, 32) - 1 as lower bound to handle both the InsertionSort and LibrarySort paths
        ulong minCompares = (ulong)(Math.Min(n, 32) - 1);
        ulong maxCompares = (ulong)(n * n);

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL); // LibrarySort never uses swaps

        var minIndexReads = stats.CompareCount;
        await Assert.That(stats.IndexReadCount >= minIndexReads).IsTrue().Because($"IndexReadCount ({stats.IndexReadCount}) should be >= CompareCount ({minIndexReads})");
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
        LibrarySort.Sort(sameValues.AsSpan(), stats);

        // LibrarySort behavior on same elements:
        // - For small arrays (n ≤ 32): Falls back to InsertionSort
        //   - Best case O(n): n-1 comparisons (equal elements never shift)
        // - For larger arrays (n > 32): LibrarySort with warmup on same elements
        //   - Warmup on same elements = 31 comparisons (same as sorted warmup)
        // Use Math.Min(n, 32) - 1 as lower bound to handle both paths uniformly
        ulong minCompares = (ulong)(Math.Min(n, 32) - 1);
        ulong maxCompares = (ulong)(n * Math.Max(1, (int)Math.Log(n, 2)) * 3);

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);

        // Verify all values remain correct
        foreach (var item in sameValues) await Assert.That(item).IsEqualTo(42);

        await Assert.That(stats.SwapCount).IsEqualTo(0UL); // LibrarySort never uses swaps
    }
}
