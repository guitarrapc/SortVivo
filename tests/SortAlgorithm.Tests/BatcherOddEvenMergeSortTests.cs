using SortAlgorithm.Algorithms;
using SortAlgorithm.Contexts;
using TUnit.Assertions.Enums;

namespace SortAlgorithm.Tests;

public class BatcherOddEvenMergeSortTests
{
    [Test]
    [MethodDataSource(typeof(MockRandomData), nameof(MockRandomData.Generate))]
    [MethodDataSource(typeof(MockPowerOfTwoRandomData), nameof(MockPowerOfTwoRandomData.Generate))]
    [MethodDataSource(typeof(MockPowerOfTwoNegativePositiveRandomData), nameof(MockPowerOfTwoNegativePositiveRandomData.Generate))]
    [MethodDataSource(typeof(MockPowerOfTwoReversedData), nameof(MockPowerOfTwoReversedData.Generate))]
    [MethodDataSource(typeof(MockPowerOfTwoNearlySortedData), nameof(MockPowerOfTwoNearlySortedData.Generate))]
    [MethodDataSource(typeof(MockPowerOfTwoSameValuesData), nameof(MockPowerOfTwoSameValuesData.Generate))]
    public async Task SortResultOrderTest(IInputSample<int> inputSample)
    {
        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();

        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);

        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    public async Task EmptyArray()
    {
        var stats = new StatisticsContext();
        var array = Array.Empty<int>();
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEmpty();
    }

    [Test]
    public async Task SingleElement()
    {
        var stats = new StatisticsContext();
        var array = new int[] { 42 };
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsSingleElement();
        await Assert.That(array[0]).IsEqualTo(42);
    }

    [Test]
    public async Task TwoElements()
    {
        var stats = new StatisticsContext();
        var array = new int[] { 2, 1 };
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEquivalentTo([1, 2], CollectionOrdering.Matching);
    }

    [Test]
    public async Task TwoElementsAlreadySorted()
    {
        var stats = new StatisticsContext();
        var array = new int[] { 1, 2 };
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEquivalentTo([1, 2], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ThreeElements()
    {
        var stats = new StatisticsContext();
        var array = new int[] { 3, 1, 2 };
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEquivalentTo([1, 2, 3], CollectionOrdering.Matching);
    }

    [Test]
    public async Task FourElements()
    {
        var stats = new StatisticsContext();
        var array = new int[] { 3, 1, 4, 2 };
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEquivalentTo([1, 2, 3, 4], CollectionOrdering.Matching);
    }

    [Test]
    public async Task EightElements()
    {
        var stats = new StatisticsContext();
        var array = new int[] { 5, 2, 8, 1, 9, 3, 7, 4 };
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEquivalentTo([1, 2, 3, 4, 5, 7, 8, 9], CollectionOrdering.Matching);
    }

    [Test]
    public async Task FiveElementsNonPowerOfTwo()
    {
        var stats = new StatisticsContext();
        var array = new int[] { 5, 3, 1, 4, 2 };
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEquivalentTo([1, 2, 3, 4, 5], CollectionOrdering.Matching);
    }

    [Test]
    public async Task SevenElementsNonPowerOfTwo()
    {
        var stats = new StatisticsContext();
        var array = new int[] { 3, 1, 4, 1, 5, 9, 2 };
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEquivalentTo([1, 1, 2, 3, 4, 5, 9], CollectionOrdering.Matching);
    }

    [Test]
    public async Task AllSameValues()
    {
        var stats = new StatisticsContext();
        var array = Enumerable.Repeat(42, 16).ToArray();
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        foreach (var item in array) await Assert.That(item).IsEqualTo(42);
    }

    [Test]
    public async Task AlreadySorted()
    {
        var stats = new StatisticsContext();
        var array = Enumerable.Range(1, 8).ToArray();
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEquivalentTo([1, 2, 3, 4, 5, 6, 7, 8], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ReverseSorted()
    {
        var stats = new StatisticsContext();
        var array = Enumerable.Range(1, 8).Reverse().ToArray();
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);
        await Assert.That(array).IsEquivalentTo([1, 2, 3, 4, 5, 6, 7, 8], CollectionOrdering.Matching);
    }

    [Test]
    public async Task StatisticsTracked()
    {
        var stats = new StatisticsContext();
        var array = new int[] { 4, 3, 2, 1 };
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);

        await Assert.That(array).IsEquivalentTo([1, 2, 3, 4], CollectionOrdering.Matching);
        await Assert.That(stats.CompareCount > 0).IsTrue();
        await Assert.That(stats.IndexReadCount > 0).IsTrue();
    }

    [Test]
    [MethodDataSource(typeof(MockPowerOfTwoSortedData), nameof(MockPowerOfTwoSortedData.Generate))]
    public async Task StatisticsSortedTest(IInputSample<int> inputSample)
    {
        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();
        BatcherOddEvenMergeSort.Sort(array.AsSpan(), stats);

        await Assert.That((ulong)array.Length).IsEqualTo((ulong)inputSample.Samples.Length);

        // Batcher Odd-Even Merge Sort has O(n log^2 n) comparisons regardless of input
        await Assert.That(stats.CompareCount > 0).IsTrue();
        await Assert.That(stats.IndexReadCount > 0).IsTrue();
    }

    [Test]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(16)]
    [Arguments(32)]
    [Arguments(64)]
    [Arguments(128)]
    public async Task TheoreticalValuesSortedTest(int n)
    {
        var stats = new StatisticsContext();
        var sorted = Enumerable.Range(0, n).ToArray();
        BatcherOddEvenMergeSort.Sort(sorted.AsSpan(), stats);

        // Batcher Odd-Even Merge Sort is data-oblivious: comparison count is independent of input order
        // For n = 2^k: C(n) = n * k * (k-1) / 4 + n - 1, where k = log2(n)
        var expectedCompares = CalculateBatcherOddEvenMergeComparisons(n);

        // Reads: each comparison reads 2 elements, each swap also reads 2 elements
        var expectedReads = expectedCompares * 2 + stats.SwapCount * 2;
        var expectedWrites = stats.SwapCount * 2;

        await Assert.That(stats.CompareCount).IsEqualTo(expectedCompares);
        await Assert.That(stats.SwapCount >= 0).IsTrue();
        await Assert.That(stats.IndexWriteCount).IsEqualTo(expectedWrites);
        await Assert.That(stats.IndexReadCount).IsEqualTo(expectedReads);
    }

    [Test]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(16)]
    [Arguments(32)]
    [Arguments(64)]
    [Arguments(128)]
    public async Task TheoreticalValuesReversedTest(int n)
    {
        var stats = new StatisticsContext();
        var reversed = Enumerable.Range(0, n).Reverse().ToArray();
        BatcherOddEvenMergeSort.Sort(reversed.AsSpan(), stats);

        // Batcher Odd-Even Merge Sort is data-oblivious: same comparison count regardless of input order
        var expectedCompares = CalculateBatcherOddEvenMergeComparisons(n);

        var expectedReads = expectedCompares * 2 + stats.SwapCount * 2;
        var expectedWrites = stats.SwapCount * 2;

        await Assert.That(stats.CompareCount).IsEqualTo(expectedCompares);
        await Assert.That(stats.SwapCount > 0).IsTrue().Because("Reversed array should require swaps");
        await Assert.That(stats.IndexWriteCount).IsEqualTo(expectedWrites);
        await Assert.That(stats.IndexReadCount).IsEqualTo(expectedReads);
    }

    [Test]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(16)]
    [Arguments(32)]
    [Arguments(64)]
    [Arguments(128)]
    public async Task TheoreticalValuesRandomTest(int n)
    {
        var stats = new StatisticsContext();
        var random = Enumerable.Range(0, n).OrderBy(_ => Guid.NewGuid()).ToArray();
        BatcherOddEvenMergeSort.Sort(random.AsSpan(), stats);

        // Batcher Odd-Even Merge Sort always performs the same number of comparisons regardless of input
        var expectedCompares = CalculateBatcherOddEvenMergeComparisons(n);

        var expectedReads = expectedCompares * 2 + stats.SwapCount * 2;
        var expectedWrites = stats.SwapCount * 2;

        await Assert.That(stats.CompareCount).IsEqualTo(expectedCompares);
        await Assert.That(stats.SwapCount >= 0).IsTrue();
        await Assert.That(stats.IndexWriteCount).IsEqualTo(expectedWrites);
        await Assert.That(stats.IndexReadCount).IsEqualTo(expectedReads);
    }

    /// <summary>
    /// Calculates the theoretical number of comparisons for Batcher's Odd-Even Merge Sort.
    /// For n = 2^k, the formula is: n * k * (k-1) / 4 + n - 1, where k = log2(n).
    /// This comes from the recursive merge structure:
    /// - The odd-even merge of two sequences of size n/2 uses fewer comparators than Bitonic Sort
    /// - For n=2: 1, n=4: 5, n=8: 19, n=16: 63, n=32: 191
    /// </summary>
    static ulong CalculateBatcherOddEvenMergeComparisons(int n)
    {
        if (n <= 1) return 0;

        var k = 0;
        var temp = n;
        while (temp > 1)
        {
            temp >>= 1;
            k++;
        }

        // Formula: n * k * (k-1) / 4 + n - 1
        return (ulong)(n * k * (k - 1) / 4 + n - 1);
    }
}
