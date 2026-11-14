using SmartX.PoE.Api.Models;

namespace SmartX.PoE.Api.DataStructures;

public sealed record BfsResult(
    string Start,
    IReadOnlyList<string> Order,
    IReadOnlyDictionary<string, int> Depth,
    IReadOnlyDictionary<string, string> Parent);

public sealed record MstResult(
    string Root,
    IReadOnlyList<MeshEdge> Edges,
    double TotalCost,
    IReadOnlyCollection<string> Reached,
    IReadOnlyDictionary<string, MeshEdge> ParentEdge);

public sealed record ShortestPathTree(
    string Root,
    IReadOnlyDictionary<string, double> Distance,
    IReadOnlyDictionary<string, MeshEdge> ParentEdge);


public static class MeshAlgorithms
{
    
    public static BfsResult Bfs(MeshGraph graph, string startId, Func<MeshNode, bool>? include = null)
    {
        include ??= _ => true;
        MeshNode start = graph.GetNode(startId);

        var order = new List<string>();
        var depth = new Dictionary<string, int>(StringComparer.Ordinal) { [start.Id] = 0 };
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(start.Id);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            order.Add(current);

            foreach (MeshEdge edge in graph.Edges(current))
            {
                if (depth.ContainsKey(edge.To)) continue;
                if (!include(graph.GetNode(edge.To))) continue; 

                depth[edge.To] = depth[current] + 1;
                parent[edge.To] = current;
                queue.Enqueue(edge.To);
            }
        }

        return new BfsResult(start.Id, order, depth, parent);
    }

    public static List<string> DfsSubtree(IReadOnlyDictionary<string, List<string>> children, string rootId)
    {
        var visited = new List<string>();
        var stack = new Stack<string>();
        stack.Push(rootId);

        while (stack.Count > 0)
        {
            string current = stack.Pop();
            visited.Add(current);
            if (!children.TryGetValue(current, out List<string>? kids)) continue;
            foreach (string kid in kids) stack.Push(kid);
        }

        return visited;
    }

    public static Dictionary<string, List<string>> ChildrenOf(MstResult tree)
    {
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (MeshEdge edge in tree.Edges)
        {
            if (!children.TryGetValue(edge.From, out List<string>? list))
            {
                list = new List<string>();
                children[edge.From] = list;
            }

            list.Add(edge.To);
        }

        return children;
    }

    public static MstResult Prim(MeshGraph graph, string rootId, Func<MeshNode, bool>? include = null)
    {
        include ??= _ => true;
        MeshNode root = graph.GetNode(rootId);

        var visited = new HashSet<string>(StringComparer.Ordinal) { root.Id };
        var treeEdges = new List<MeshEdge>();
        var parentEdge = new Dictionary<string, MeshEdge>(StringComparer.Ordinal);
        var heap = new MinHeap<MeshEdge>(EdgeOrder);
        double total = 0;

        void PushEdgesOf(string nodeId)
        {
            foreach (MeshEdge edge in graph.Edges(nodeId))
            {
                if (visited.Contains(edge.To)) continue;
                if (!include(graph.GetNode(edge.To))) continue;
                heap.Push(edge);
            }
        }

        PushEdgesOf(root.Id);

        while (!heap.IsEmpty)
        {
            MeshEdge cheapest = heap.Pop();
            if (!visited.Add(cheapest.To)) continue; 

            treeEdges.Add(cheapest);
            parentEdge[cheapest.To] = cheapest;
            total += cheapest.Cost;
            PushEdgesOf(cheapest.To);
        }

        return new MstResult(root.Id, treeEdges, total, visited, parentEdge);
    }

    public static ShortestPathTree Dijkstra(MeshGraph graph, string rootId, Func<MeshNode, bool>? include = null)
    {
        include ??= _ => true;
        MeshNode root = graph.GetNode(rootId);

        var distance = new Dictionary<string, double>(StringComparer.Ordinal) { [root.Id] = 0 };
        var parentEdge = new Dictionary<string, MeshEdge>(StringComparer.Ordinal);
        var settled = new HashSet<string>(StringComparer.Ordinal);
        var heap = new MinHeap<(double Dist, string Node)>(
            Comparer<(double Dist, string Node)>.Create((a, b) =>
            {
                int byDist = a.Dist.CompareTo(b.Dist);
                return byDist != 0 ? byDist : string.CompareOrdinal(a.Node, b.Node);
            }));
        heap.Push((0, root.Id));

        while (!heap.IsEmpty)
        {
            (double dist, string current) = heap.Pop();
            if (!settled.Add(current)) continue; 

            foreach (MeshEdge edge in graph.Edges(current))
            {
                if (settled.Contains(edge.To)) continue;
                if (!include(graph.GetNode(edge.To))) continue;

                double candidate = dist + edge.Cost;
                if (distance.TryGetValue(edge.To, out double known) && known <= candidate) continue;

                distance[edge.To] = candidate;
                parentEdge[edge.To] = edge;
                heap.Push((candidate, edge.To));
            }
        }

        return new ShortestPathTree(root.Id, distance, parentEdge);
    }


    public static RoutePath? RouteToRoot(IReadOnlyDictionary<string, MeshEdge> parentEdge, string root, string nodeId)
    {
        if (nodeId == root) return new RoutePath(new[] { root }, 0);
        if (!parentEdge.ContainsKey(nodeId)) return null;

        var hops = new List<string> { nodeId };
        double cost = 0;
        string current = nodeId;

        while (current != root)
        {
            MeshEdge edge = parentEdge[current];
            cost += edge.Cost;
            current = edge.From;
            hops.Add(current);
        }

        return new RoutePath(hops, cost);
    }

    public static RoutePath? RouteToRoot(BfsResult bfs, MeshGraph graph, string nodeId)
    {
        if (!bfs.Depth.ContainsKey(nodeId)) return null;

        var hops = new List<string> { nodeId };
        double cost = 0;
        string current = nodeId;

        while (current != bfs.Start)
        {
            string next = bfs.Parent[current];
            cost += graph.Edges(current).First(e => e.To == next).Cost;
            current = next;
            hops.Add(current);
        }

        return new RoutePath(hops, cost);
    }

    private static readonly IComparer<MeshEdge> EdgeOrder = Comparer<MeshEdge>.Create((a, b) =>
    {
        int byCost = a.Cost.CompareTo(b.Cost);
        if (byCost != 0) return byCost;
        int byFrom = string.CompareOrdinal(a.From, b.From);
        return byFrom != 0 ? byFrom : string.CompareOrdinal(a.To, b.To);
    });
}
