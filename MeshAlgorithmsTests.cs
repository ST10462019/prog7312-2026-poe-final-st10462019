using SmartX.PoE.Api.DataStructures;
using SmartX.PoE.Api.Models;
using Xunit;

namespace SmartX.PoE.Tests;

public class MeshAlgorithmsTests
{
    private static MeshGraph RandomConnectedGraph(int seed, int nodeCount)
    {
        var random = new Random(seed);
        var graph = new MeshGraph();
        for (int i = 0; i < nodeCount; i++)
            graph.AddNode(new MeshNode($"V{i}", $"AA:BB:CC:DD:00:{i:X2}", $"Node {i}", "test", isGateway: i == 0));

        for (int i = 1; i < nodeCount; i++)
            graph.AddLink($"V{random.Next(0, i)}", $"V{i}", Math.Round(1 + random.NextDouble() * 19, 2));

        for (int extra = 0; extra < nodeCount * 2; extra++)
        {
            int a = random.Next(nodeCount), b = random.Next(nodeCount);
            if (a == b) continue;
            try { graph.AddLink($"V{a}", $"V{b}", Math.Round(1 + random.NextDouble() * 19, 2)); }
            catch (ArgumentException) { /* link already exists */ }
        }

        return graph;
    }

    private static double KruskalTotal(MeshGraph graph)
    {
        var parent = graph.Nodes.ToDictionary(n => n.Id, n => n.Id);
        string Find(string x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }

        double total = 0;
        foreach (MeshEdge edge in graph.Links().OrderBy(e => e.Cost))
        {
            string a = Find(edge.From), b = Find(edge.To);
            if (a == b) continue;
            parent[a] = b;
            total += edge.Cost;
        }

        return total;
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 25)]
    [InlineData(3, 40)]
    [InlineData(4, 80)]
    public void Prim_TotalCostEqualsKruskal_OnRandomGraphs(int seed, int size)
    {
        MeshGraph graph = RandomConnectedGraph(seed, size);

        MstResult mst = MeshAlgorithms.Prim(graph, "V0");

        Assert.Equal(size - 1, mst.Edges.Count);
        Assert.Equal(size, mst.Reached.Count);
        Assert.Equal(KruskalTotal(graph), mst.TotalCost, 6);
    }

    [Fact]
    public void Prim_SkipsNodesRejectedByTheFilter()
    {
        MeshGraph graph = RandomConnectedGraph(5, 20);
        graph.GetNode("V7").Status = NodeStatus.Offline;

        MstResult mst = MeshAlgorithms.Prim(graph, "V0", n => n.IsOnline);

        Assert.DoesNotContain("V7", mst.Reached);
        Assert.DoesNotContain(mst.Edges, e => e.From == "V7" || e.To == "V7");
    }

    [Fact]
    public void Bfs_DepthIsTheFewestHops()
    {
        var graph = new MeshGraph();
        foreach (string id in new[] { "G", "A", "B", "C", "D" })
            graph.AddNode(new MeshNode(id, $"AA:00:00:00:00:{id[0] % 256:X2}", id, "t", id == "G"));
        graph.AddLink("G", "A", 1);
        graph.AddLink("A", "B", 1);
        graph.AddLink("B", "C", 1);
        graph.AddLink("G", "D", 9);   
        graph.AddLink("D", "C", 9);

        BfsResult bfs = MeshAlgorithms.Bfs(graph, "G");

        Assert.Equal(0, bfs.Depth["G"]);
        Assert.Equal(1, bfs.Depth["D"]);
        Assert.Equal(2, bfs.Depth["C"]);  
        Assert.Equal("G", bfs.Order[0]);
        Assert.Equal(5, bfs.Order.Count);
    }

    [Fact]
    public void Bfs_DoesNotCrossAnOfflineNode()
    {
        var graph = new MeshGraph();
        foreach (string id in new[] { "G", "A", "B" })
            graph.AddNode(new MeshNode(id, $"AA:00:00:00:00:{id[0] % 256:X2}", id, "t", id == "G"));
        graph.AddLink("G", "A", 1);
        graph.AddLink("A", "B", 1);
        graph.GetNode("A").Status = NodeStatus.Offline;

        BfsResult bfs = MeshAlgorithms.Bfs(graph, "G", n => n.IsOnline);

        Assert.Equal(new[] { "G" }, bfs.Order);
        Assert.False(bfs.Depth.ContainsKey("B"));
    }

    [Fact]
    public void Dijkstra_CanBeCheaperThanTheMstRoute()
    {

        var graph = new MeshGraph();
        graph.AddNode(new MeshNode("G", "AA:00:00:00:00:01", "G", "t", true));
        graph.AddNode(new MeshNode("A", "AA:00:00:00:00:02", "A", "t"));
        graph.AddNode(new MeshNode("B", "AA:00:00:00:00:03", "B", "t"));
        graph.AddLink("G", "A", 5);
        graph.AddLink("G", "B", 3);
        graph.AddLink("B", "A", 3);

        MstResult mst = MeshAlgorithms.Prim(graph, "G");
        ShortestPathTree spt = MeshAlgorithms.Dijkstra(graph, "G");

        Assert.Equal(6, mst.TotalCost, 6);
        Assert.Equal(6, MeshAlgorithms.RouteToRoot(mst.ParentEdge, "G", "A")!.TotalCost, 6);
        Assert.Equal(5, spt.Distance["A"], 6);
        Assert.Equal(new[] { "A", "G" }, MeshAlgorithms.RouteToRoot(spt.ParentEdge, "G", "A")!.Hops);
    }

    [Fact]
    public void RouteToRoot_ReturnsNullForAnUnreachedNode()
    {
        MeshGraph graph = RandomConnectedGraph(9, 12);
        graph.GetNode("V3").Status = NodeStatus.Offline;

        MstResult mst = MeshAlgorithms.Prim(graph, "V0", n => n.IsOnline);

        Assert.Null(MeshAlgorithms.RouteToRoot(mst.ParentEdge, "V0", "V3"));
        Assert.Equal(new[] { "V0" }, MeshAlgorithms.RouteToRoot(mst.ParentEdge, "V0", "V0")!.Hops);
    }

    [Fact]
    public void DfsSubtree_ListsEveryDescendant()
    {
        var children = new Dictionary<string, List<string>>
        {
            ["R"] = new() { "A", "B" },
            ["A"] = new() { "C", "D" },
            ["D"] = new() { "E" }
        };

        List<string> subtree = MeshAlgorithms.DfsSubtree(children, "A");

        Assert.Equal(new[] { "A", "C", "D", "E" }, subtree.OrderBy(x => x));
    }

    [Theory]
    [InlineData("24:6F:28:A1:00:04")]
    [InlineData("24-6f-28-a1-00-04")]
    [InlineData("246f28a10004")]
    public void MacAddresses_ResolveInAnyCommonFormat(string mac)
    {
        var graph = new MeshGraph();
        graph.AddNode(new MeshNode("N-04", "24:6F:28:A1:00:04", "Sensor 04", "z"));

        Assert.True(graph.TryResolve(mac, out MeshNode node));
        Assert.Equal("N-04", node.Id);
    }

    [Fact]
    public void AddNode_RejectsDuplicateMac()
    {
        var graph = new MeshGraph();
        graph.AddNode(new MeshNode("A", "24:6F:28:A1:00:01", "A", "z"));
        Assert.Throws<ArgumentException>(() => graph.AddNode(new MeshNode("B", "24-6f-28-a1-00-01", "B", "z")));
    }
}
