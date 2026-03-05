using SortAlgorithm.Algorithms;
using SortAlgorithm.Contexts;
using TUnit.Assertions.Enums;

namespace SortAlgorithm.Tests;

public class SymMergeSortTests
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
        Skip.When(inputSample.Samples.Length > 1024, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        SymMergeSort.Sort(array.AsSpan(), stats);

        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockNanRandomData), nameof(MockNanRandomData.GenerateHalf))]
    public async Task SortHalfResultOrderTest(IInputSample<Half> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 1024, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        SymMergeSort.Sort(array.AsSpan(), stats);

        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockNanRandomData), nameof(MockNanRandomData.GenerateFloat))]
    public async Task SortFloatResultOrderTest(IInputSample<float> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 1024, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        SymMergeSort.Sort(array.AsSpan(), stats);

        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockNanRandomData), nameof(MockNanRandomData.GenerateDouble))]
    public async Task SortDoubleResultOrderTest(IInputSample<double> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 1024, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        SymMergeSort.Sort(array.AsSpan(), stats);

        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockIntKeyRandomData), nameof(MockIntKeyRandomData.Generate))]
    public async Task SortIntStructResultOrderTest(IInputSample<Utils.IntKey> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 1024, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        SymMergeSort.Sort(array.AsSpan(), stats);

        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockStabilityData), nameof(MockStabilityData.Generate))]
    public async Task StabilityTest(StabilityTestItem[] items)
    {
        // Test stability: equal elements should maintain relative order
        var stats = new StatisticsContext();

        SymMergeSort.Sort(items.AsSpan(), stats);

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

        SymMergeSort.Sort(items.AsSpan(), stats);

        // Expected: [2:B, 2:D, 2:F, 5:A, 5:C, 5:G, 8:E]
        // Keys are sorted, and elements with the same key maintain original order

        for (var i = 0; i < items.Length; i++)
        {
            await Assert.That(items[i].Key).IsEqualTo(MockStabilityWithIdData.Sorted[i].Key);
            await Assert.That(items[i].Id).IsEqualTo(MockStabilityWithIdData.Sorted[i].Id);
        }
    }

    [Test]
    [MethodDataSource(typeof(MockStabilityAllEqualsData), nameof(MockStabilityAllEqualsData.Generate))]
    public async Task StabilityTestWithAllEqual(StabilityTestItem[] items)
    {
        // Edge case: all elements have the same value
        var stats = new StatisticsContext();

        var originalOrder = items.Select(x => x.OriginalIndex).ToArray();

        SymMergeSort.Sort(items.AsSpan(), stats);

        var resultOrder = items.Select(x => x.OriginalIndex).ToArray();
        await Assert.That(resultOrder).IsEquivalentTo(originalOrder, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockSortedData), nameof(MockSortedData.Generate))]
    public async Task StatisticsSortedTest(IInputSample<int> inputSample)
    {
        if (inputSample.Samples.Length > 1024)
            return;

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();
        SymMergeSort.Sort(array.AsSpan(), stats);

        await Assert.That((ulong)array.Length).IsEqualTo((ulong)inputSample.Samples.Length);
        await Assert.That(stats.IndexReadCount).IsNotEqualTo(0UL);
        await Assert.That(stats.IndexWriteCount).IsEqualTo(0UL);  // Sorted data: no writes needed
        await Assert.That(stats.CompareCount).IsNotEqualTo(0UL);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL);        // Sorted data: no swaps needed
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
        SymMergeSort.Sort(sorted.AsSpan(), stats);

        // Bottom-up SymMergeSort on sorted data:
        //
        // Phase 1 (InsertionSort blocks): for sorted data, each block costs (blockSize-1) comparisons
        //   with no writes or swaps. Sum across all blocks = n - ⌈n/threshold⌉.
        //
        // Phase 2 (merge passes): each adjacent pair passes the already-sorted skip-check
        //   (s[mid-1] ≤ s[mid]) in exactly 1 comparison, so SymMerge is never entered.
        //   Total skip-check comparisons ≈ ⌊n/32⌋ + ⌊n/64⌋ + … ≈ n/threshold.
        //
        // Combined total is always n-1 comparisons.
        //
        // Observed: n=10 → 9, n=20 → 19, n=50 → 49, n=100 → 99
        var minCompares = (ulong)(n - 1);
        var maxCompares = (ulong)(n);

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);
        await Assert.That(stats.IndexWriteCount).IsEqualTo(0UL);  // No writes on sorted data
        await Assert.That(stats.SwapCount).IsEqualTo(0UL);        // No rotations on sorted data
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
        SymMergeSort.Sort(reversed.AsSpan(), stats);

        // Bottom-up SymMergeSort on reversed data:
        //
        // Phase 1 (InsertionSort blocks): n ≤ 16 → single block, all work done here.
        //   n*(n-1)/2 comparisons and writes, 0 swaps (InsertionSort uses Write, not Swap).
        //
        // Phase 2 (SymMerge passes): symmetric binary search (O(log n) per call) + rotation.
        //   3-reversal uses Swap for the general case; k==1/k==n-1 fast paths use Write.
        //
        // Actual observations for reversed data:
        //   n=10:   45 comparisons (n*(n-1)/2, InsertionSort only)
        //   n=20:  162 comparisons (~1.88 * n*log₂n)
        //   n=50:  453 comparisons (~1.61 * n*log₂n)
        //   n=100: 821 comparisons (~1.24 * n*log₂n)
        var logN = Math.Log2(n);
        var minCompares = n <= 16 ? (ulong)(n * 4.0) : (ulong)(n * logN * 0.9);
        var maxCompares = n <= 16 ? (ulong)(n * 5.5) : (ulong)(n * logN * 2.5);

        var minWrites = n <= 16 ? (ulong)(n * 4.0) : (ulong)(n * logN * 1.0);
        var maxWrites = n <= 16 ? (ulong)(n * 6.0) : (ulong)(n * logN * 20.0);

        var minSwaps = 0UL;
        var maxSwaps = n <= 16 ? 0UL : (ulong)(n * logN * 2.0);

        var minReads = (ulong)(stats.CompareCount * 1.2);

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);
        await Assert.That(stats.IndexWriteCount).IsBetween(minWrites, maxWrites);
        await Assert.That(stats.SwapCount).IsBetween(minSwaps, maxSwaps);
        await Assert.That(stats.IndexReadCount >= minReads).IsTrue().Because($"IndexReadCount ({stats.IndexReadCount}) should be >= {minReads}");
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
        SymMergeSort.Sort(random.AsSpan(), stats);

        // Bottom-up SymMergeSort on random data:
        //
        // Phase 1 (InsertionSort blocks): n ≤ 16 → single block, variance depends on order.
        // Phase 2 (SymMerge passes): symmetric binary search does not benefit from galloping,
        //   so comparisons are higher than RotateMerge for these sizes.
        //
        // Actual observations over 5 random runs:
        //   n=10:    27–34  comparisons (InsertionSort only, varies with order)
        //   n=20:    93–135 comparisons (~1.1–1.6 * n*log₂n)
        //   n=50:   421–496 comparisons (~1.5–1.8 * n*log₂n)
        //   n=100: 1213–1313 comparisons (~1.8–2.0 * n*log₂n)
        var logN = Math.Log2(n);
        var minCompares = n <= 16 ? (ulong)(n * 0.8) : (ulong)(n * logN * 0.7);
        var maxCompares = n <= 16 ? (ulong)(n * 4.8) : (ulong)(n * logN * 2.5);

        var minWrites = n <= 16 ? (ulong)(n * 0.8) : (ulong)(n * logN * 0.5);
        var maxWrites = n <= 16 ? (ulong)(n * 4.8) : (ulong)(n * logN * 10.0);

        var minSwaps = 0UL;
        var maxSwaps = n <= 16 ? 0UL : (ulong)(n * logN * 1.5);

        var minReads = (ulong)(stats.CompareCount * 1.2);

        await Assert.That(stats.CompareCount).IsBetween(minCompares, maxCompares);
        await Assert.That(stats.IndexWriteCount).IsBetween(minWrites, maxWrites);
        await Assert.That(stats.SwapCount).IsBetween(minSwaps, maxSwaps);
        await Assert.That(stats.IndexReadCount >= minReads).IsTrue().Because($"IndexReadCount ({stats.IndexReadCount}) should be >= {minReads}");
    }
}
