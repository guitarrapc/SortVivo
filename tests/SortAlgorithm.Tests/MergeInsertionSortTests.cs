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
}
