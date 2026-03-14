using SortAlgorithm.Algorithms;
using SortAlgorithm.Contexts;
using TUnit.Assertions.Enums;

namespace SortAlgorithm.Tests;

public class PatienceSortTests
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

        PatienceSort.Sort(array.AsSpan(), stats);

        // Check is sorted
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

        PatienceSort.Sort(array.AsSpan(), stats);

        // Check is sorted
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

        PatienceSort.Sort(array.AsSpan(), stats);

        // Check is sorted
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

        PatienceSort.Sort(array.AsSpan(), stats);

        // Check is sorted
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

        PatienceSort.Sort(array.AsSpan(), stats);

        // Check is sorted
        Array.Sort(inputSample.Samples);
        await Assert.That(array).IsEquivalentTo(inputSample.Samples, CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(MockSortedData), nameof(MockSortedData.Generate))]
    public async Task StatisticsSortedTest(IInputSample<int> inputSample)
    {
        Skip.When(inputSample.Samples.Length > 1024, "Skip large inputs for order test");

        var stats = new StatisticsContext();
        var array = inputSample.Samples.ToArray();
        PatienceSort.Sort(array.AsSpan(), stats);

        var n = (ulong)array.Length;
        await Assert.That(n).IsEqualTo((ulong)inputSample.Samples.Length);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL);
        if (inputSample.Samples.Length <= 1)
        {
            await Assert.That(stats.CompareCount).IsEqualTo(0UL);
            await Assert.That(stats.IndexWriteCount).IsEqualTo(0UL);
        }
        else
        {
            // Sorted input creates n piles (worst case for PatienceSort — opposite of most algorithms).
            // IndexWriteCount = 2n for all inputs: n writes to merge buffer + n writes back via CopyTo
            await Assert.That(stats.IndexWriteCount).IsEqualTo(2 * n);
            // IndexReadCount = 2n + 2*CompareCount: every Compare(int,int) reads 2 elements from main span,
            // plus n reads in the extract loop (s.Read(topIdx)) and n reads from the final CopyTo (merge→s)
            await Assert.That(stats.IndexReadCount).IsEqualTo(2 * n + 2 * stats.CompareCount);
        }
    }

    [Test]
    [Arguments(10)]
    [Arguments(20)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task TheoreticalValuesSortedTest(int n)
    {
        var stats = new StatisticsContext();
        // Sorted input creates n piles (worst case for PatienceSort):
        // each element is larger than all pile tops, so every element starts a new pile.
        var sorted = Enumerable.Range(0, n).ToArray();
        PatienceSort.Sort(sorted.AsSpan(), stats);

        // IndexWriteCount = 2n for all inputs:
        // n writes to merge buffer (extract loop) + n writes back to main span (CopyTo)
        var expectedWrites = 2 * (ulong)n;
        await Assert.That(stats.IndexWriteCount).IsEqualTo(expectedWrites);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL);

        // IndexReadCount = 2n + 2*CompareCount (exact for all inputs):
        // all compares use Compare(int,int) which reads 2 elements each,
        // plus n reads from s.Read(topIdx) in extract loop and n reads from merge in CopyTo
        await Assert.That(stats.IndexReadCount).IsEqualTo(2 * (ulong)n + 2 * stats.CompareCount);

        // Sorted creates n piles: Phase1 binary search contributes ≥ n-1 compares,
        // and build-heap contributes exactly n-1 compares → total ≥ 2*(n-1)
        await Assert.That(stats.CompareCount >= 2 * (ulong)(n - 1)).IsTrue()
            .Because($"Sorted input (n piles) should have CompareCount >= 2*(n-1)={2 * (n - 1)}, but got {stats.CompareCount}");
    }

    [Test]
    [Arguments(10)]
    [Arguments(20)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task TheoreticalValuesReversedTest(int n)
    {
        var stats = new StatisticsContext();
        // Reversed input creates 1 pile (best case for PatienceSort):
        // each element is smaller than the current pile top, so all elements stack onto pile 0.
        var reversed = Enumerable.Range(0, n).Reverse().ToArray();
        PatienceSort.Sort(reversed.AsSpan(), stats);

        // 1 pile → Phase1 binary search: n-1 compares (1 per element for i=1..n-1),
        //          Phase2 build-heap: 0 compares (loop doesn't execute for pileCount=1),
        //          Phase2 extract: 0 compares (HeapifyDown with size=1 exits immediately)
        var expectedCompares = (ulong)(n - 1);
        // IndexWriteCount = 2n for all inputs
        var expectedWrites = 2 * (ulong)n;
        // IndexReadCount = 2n + 2*(n-1) = 4n-2
        var expectedReads = 4 * (ulong)n - 2;

        await Assert.That(stats.CompareCount).IsEqualTo(expectedCompares);
        await Assert.That(stats.IndexWriteCount).IsEqualTo(expectedWrites);
        await Assert.That(stats.IndexReadCount).IsEqualTo(expectedReads);
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
        PatienceSort.Sort(random.AsSpan(), stats);

        // IndexWriteCount = 2n for all inputs (invariant regardless of pile count)
        await Assert.That(stats.IndexWriteCount).IsEqualTo(2 * (ulong)n);
        await Assert.That(stats.SwapCount).IsEqualTo(0UL);

        // Minimum CompareCount = reversed case (1 pile, only binary search compares)
        await Assert.That(stats.CompareCount >= (ulong)(n - 1)).IsTrue()
            .Because($"CompareCount ({stats.CompareCount}) should be >= n-1={n - 1}");

        // IndexReadCount invariant holds for any input regardless of pile structure
        await Assert.That(stats.IndexReadCount).IsEqualTo(2 * (ulong)n + 2 * stats.CompareCount);
    }
}
