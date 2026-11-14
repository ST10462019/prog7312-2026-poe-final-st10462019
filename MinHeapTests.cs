using SmartX.PoE.Api.DataStructures;
using Xunit;

namespace SmartX.PoE.Tests;

public class MinHeapTests
{
    [Fact]
    public void Pop_ReturnsItemsInAscendingOrder()
    {
        var random = new Random(3);
        var heap = new MinHeap<int>();
        var input = Enumerable.Range(0, 1_000).Select(_ => random.Next(0, 500)).ToList();
        foreach (int x in input) heap.Push(x);

        var output = new List<int>();
        while (!heap.IsEmpty) output.Add(heap.Pop());

        Assert.Equal(input.OrderBy(x => x).ToList(), output);
    }

    [Fact]
    public void Peek_DoesNotRemove()
    {
        var heap = new MinHeap<int>();
        heap.Push(4);
        heap.Push(2);

        Assert.Equal(2, heap.Peek());
        Assert.Equal(2, heap.Count);
    }

    [Fact]
    public void EmptyHeap_ThrowsOnPopAndPeek()
    {
        var heap = new MinHeap<int>();
        Assert.Throws<InvalidOperationException>(() => heap.Pop());
        Assert.Throws<InvalidOperationException>(() => heap.Peek());
    }

    [Fact]
    public void CustomComparer_CanMakeAMaxHeap()
    {
        var heap = new MinHeap<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        foreach (int x in new[] { 3, 9, 1, 5 }) heap.Push(x);

        Assert.Equal(9, heap.Pop());
        Assert.Equal(5, heap.Pop());
    }
}
