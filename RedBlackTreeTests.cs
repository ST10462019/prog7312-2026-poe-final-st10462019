using SmartX.PoE.Api.DataStructures;
using Xunit;

namespace SmartX.PoE.Tests;

public class RedBlackTreeTests
{
    [Fact]
    public void Insert_AscendingKeys_StaysBalancedAndValid()
    {
       
        var tree = new RedBlackTree<int, string>();
        for (int i = 0; i < 10_000; i++) tree.Insert(i, i.ToString());

        Assert.Equal(10_000, tree.Count);
        Assert.True(tree.IsValid());
        Assert.True(tree.Height() <= 2 * Math.Ceiling(Math.Log2(tree.Count + 1)));
    }

    [Fact]
    public void Insert_RandomKeys_KeepsEveryInvariant()
    {
        var random = new Random(42);
        var tree = new RedBlackTree<int, int>();
        for (int i = 0; i < 5_000; i++) tree.Insert(random.Next(), i);

        Assert.True(tree.IsValid());
        Assert.Equal(tree.Count, tree.InOrder().Count);
    }

    [Fact]
    public void Insert_DuplicateKey_ReplacesValueWithoutGrowing()
    {
        var tree = new RedBlackTree<int, string>();
        tree.Insert(7, "first");
        tree.Insert(7, "second");

        Assert.Equal(1, tree.Count);
        Assert.True(tree.TryGetValue(7, out string? value));
        Assert.Equal("second", value);
    }

    [Fact]
    public void InOrder_ReturnsValuesSortedByKey()
    {
        var tree = new RedBlackTree<int, int>();
        foreach (int k in new[] { 50, 20, 80, 10, 30, 70, 90, 60 }) tree.Insert(k, k);

        Assert.Equal(new[] { 10, 20, 30, 50, 60, 70, 80, 90 }, tree.InOrder());
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(250, 400)]
    [InlineData(-50, 10)]
    [InlineData(990, 2000)]
    [InlineData(500, 500)]
    [InlineData(2000, 3000)]
    public void Range_MatchesAPlainLinearFilter(int low, int high)
    {
        var random = new Random(7);
        var tree = new RedBlackTree<int, int>();
        var reference = new SortedDictionary<int, int>();
        for (int i = 0; i < 800; i++)
        {
            int key = random.Next(0, 1000);
            tree.Insert(key, key);
            reference[key] = key;
        }

        List<int> expected = reference.Where(kv => kv.Key >= low && kv.Key <= high).Select(kv => kv.Value).ToList();
        Assert.Equal(expected, tree.Range(low, high));
    }

    [Fact]
    public void Range_WithLowAboveHigh_IsEmpty()
    {
        var tree = new RedBlackTree<int, int>();
        tree.Insert(1, 1);
        Assert.Empty(tree.Range(10, 5));
    }

    [Fact]
    public void MinAndMax_AreFoundOnTheTreeEdges()
    {
        var tree = new RedBlackTree<int, string>();
        Assert.False(tree.TryGetMin(out _, out _));

        foreach (int k in new[] { 5, 3, 9, 1, 7 }) tree.Insert(k, $"v{k}");

        Assert.True(tree.TryGetMin(out int min, out string? minValue));
        Assert.True(tree.TryGetMax(out int max, out string? maxValue));
        Assert.Equal(1, min);
        Assert.Equal("v1", minValue);
        Assert.Equal(9, max);
        Assert.Equal("v9", maxValue);
    }
}
